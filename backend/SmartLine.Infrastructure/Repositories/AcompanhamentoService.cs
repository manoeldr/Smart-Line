using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Coleta;
using SmartLine.Core.Entities.Global;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class AcompanhamentoService : IAcompanhamentoService
{
    private readonly SmartLineDbContext _context;
    private readonly IRegrasPadraoService _regrasPadrao;
    private readonly TimeProvider _tempo;
    private readonly OpcoesColetaIot _opcoes;

    public AcompanhamentoService(
        SmartLineDbContext context,
        IRegrasPadraoService regrasPadrao,
        TimeProvider tempo,
        OpcoesColetaIot opcoes)
    {
        _context = context;
        _regrasPadrao = regrasPadrao;
        _tempo = tempo;
        _opcoes = opcoes;
    }

    // ── Iniciar ─────────────────────────────────────────────────────

    public async Task<ResultadoIniciarAcompanhamento> IniciarAsync(
        Guid usuarioId,
        IniciarAcompanhamentoRequest request,
        CancellationToken cancellationToken = default)
    {
        var ml = await _context.MaquinasLinha
            .FirstOrDefaultAsync(x => x.Id == request.MaquinaLinhaId, cancellationToken);

        var erro = await ValidarAsync(ml, request, cancellationToken);
        if (erro is not null)
            return new ResultadoIniciarAcompanhamento(null, erro);

        var agora = _tempo.GetUtcNow().UtcDateTime;
        var acompanhamento = new Acompanhamento
        {
            Id = Guid.NewGuid(),
            MaquinaLinhaId = ml!.Id,
            UsuarioId = usuarioId,
            IniciadoEm = agora,
            TempoDeteccaoParadaSegundos = ml.TempoDeteccaoParadaSegundos,
            Canais = request.Canais.Select(c => new AcompanhamentoCanal
            {
                Id = Guid.NewGuid(),
                Canal = c.Canal,
                Multiplicador = MapaWise.Definicao(c.Canal).Tipo == TipoCanal.Contador ? c.Multiplicador : 1
            }).ToList()
        };
        _context.Acompanhamentos.Add(acompanhamento);

        var sessao = NovaSessao(
            acompanhamento, agora,
            request.VelocidadeNominal ?? ml.VelocidadeNominal,
            request.SobreVelocidade ?? ml.SobreVelocidade);
        _context.Sessoes.Add(sessao);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Índice único "um acompanhamento em andamento por máquina": outra estação
            // iniciou nesta máquina entre a validação e a gravação.
            _context.ChangeTracker.Clear();
            return new ResultadoIniciarAcompanhamento(null,
                "Outra estação iniciou medição nesta máquina agora há pouco. Atualize e tente de novo.");
        }

        return new ResultadoIniciarAcompanhamento(
            new AcompanhamentoIniciadoDto(acompanhamento.Id, ml.Id, sessao.Id), null);
    }

    private async Task<string?> ValidarAsync(
        MaquinaLinha? ml,
        IniciarAcompanhamentoRequest request,
        CancellationToken cancellationToken)
    {
        if (ml is null || !ml.Ativo)
            return "Máquina não encontrada ou inativa.";

        if (!await _context.DispositivosIot.AnyAsync(d => d.Ativo && d.MaquinaLinhaId == ml.Id, cancellationToken))
            return "Nenhum WISE ativo vinculado a esta máquina.";

        // Só esta máquina: as outras da linha continuam livres (inclusive para o Manual).
        var ocupada =
            await _context.Acompanhamentos.AnyAsync(a => a.FinalizadoEm == null && a.MaquinaLinhaId == ml.Id, cancellationToken)
            || await _context.Sessoes.AnyAsync(s => s.Status == StatusSessao.EmAndamento && s.MaquinaLinhaId == ml.Id, cancellationToken);
        if (ocupada)
            return "Já existe uma medição em andamento nesta máquina.";

        // A validação dos canais e das regras é a mesma que o coletor vai usar: montar a
        // configuração de verdade garante que o que foi aceito aqui roda lá.
        try
        {
            await MontarConfiguracaoAsync(
                ml,
                request.Canais.Select(c => new CanalMedicao(c.Canal, c.Multiplicador)),
                ml.TempoDeteccaoParadaSegundos,
                cancellationToken);
            return null;
        }
        catch (ArgumentException ex)
        {
            return ex.ParamName is null ? ex.Message : ex.Message.Replace($" (Parameter '{ex.ParamName}')", "");
        }
        catch (InvalidOperationException ex)
        {
            return $"Regras de classificação inconsistentes: {ex.Message}";
        }
    }

    // ── Finalizar ───────────────────────────────────────────────────

    public async Task<ResultadoFinalizacao> FinalizarAsync(
        Guid acompanhamentoId,
        Guid usuarioId,
        bool podeFinalizarDeOutros,
        CancellationToken cancellationToken = default)
    {
        var acompanhamento = await _context.Acompanhamentos
            .FirstOrDefaultAsync(a => a.Id == acompanhamentoId, cancellationToken);

        if (acompanhamento is null)
            return ResultadoFinalizacao.NaoEncontrado;
        if (acompanhamento.FinalizadoEm is not null)
            return ResultadoFinalizacao.JaFinalizado;
        if (acompanhamento.UsuarioId != usuarioId && !podeFinalizarDeOutros)
            return ResultadoFinalizacao.SemPermissao;

        var agora = _tempo.GetUtcNow().UtcDateTime;
        acompanhamento.FinalizadoEm = agora;
        acompanhamento.FinalizadoPorId = usuarioId;

        var sessao = await _context.Sessoes
            .Include(s => s.Paradas)
            .FirstOrDefaultAsync(s => s.AcompanhamentoId == acompanhamentoId
                                      && s.Status == StatusSessao.EmAndamento, cancellationToken);
        if (sessao is not null)
        {
            foreach (var parada in sessao.Paradas.Where(p => p.Fim is null))
                parada.Fim = agora;

            sessao.Fim = agora;
            sessao.Status = StatusSessao.Finalizada;
            sessao.MotivoFechamento = MotivoFechamentoSessao.Manual;
        }

        var periodosAbertos = await _context.PeriodosSemComunicacao
            .Where(p => p.MaquinaLinhaId == acompanhamento.MaquinaLinhaId && p.Fim == null)
            .ToListAsync(cancellationToken);
        foreach (var periodo in periodosAbertos)
            periodo.Fim = agora;

        await _context.SaveChangesAsync(cancellationToken);
        return ResultadoFinalizacao.Finalizado;
    }

    // ── Coletas em andamento ────────────────────────────────────────

    public async Task<IReadOnlyList<ColetaIotResumoDto>> ListarEmAndamentoAsync(
        Guid? maquinaLinhaId = null,
        CancellationToken cancellationToken = default)
    {
        var acompanhamentos = await _context.Acompanhamentos
            .AsNoTracking()
            .Where(a => a.FinalizadoEm == null && (maquinaLinhaId == null || a.MaquinaLinhaId == maquinaLinhaId))
            .Select(a => new
            {
                a.Id,
                a.MaquinaLinhaId,
                a.MaquinaLinha.MaquinaId,
                Maquina = a.MaquinaLinha.Maquina.Nome,
                a.MaquinaLinha.LinhaId,
                Linha = a.MaquinaLinha.Linha.Nome,
                Cliente = a.MaquinaLinha.Linha.Cliente.Nome,
                a.MaquinaLinha.Ordem,
                a.UsuarioId,
                Usuario = a.Usuario.Nome,
                a.IniciadoEm,
                a.TempoDeteccaoParadaSegundos,
                Canais = a.Canais.OrderBy(c => c.Canal).Select(c => new { c.Canal, c.Multiplicador }).ToList()
            })
            .ToListAsync(cancellationToken);

        var resumos = new List<ColetaIotResumoDto>();
        foreach (var a in acompanhamentos.OrderBy(a => a.Cliente).ThenBy(a => a.Linha).ThenBy(a => a.Ordem))
        {
            var ip = await _context.DispositivosIot
                .Where(d => d.MaquinaLinhaId == a.MaquinaLinhaId)
                .Select(d => d.EnderecoIp)
                .FirstOrDefaultAsync(cancellationToken);

            var sessao = await _context.Sessoes
                .AsNoTracking()
                .Where(s => s.AcompanhamentoId == a.Id && s.Status == StatusSessao.EmAndamento)
                .Select(s => new { s.Id, s.Inicio, s.VelocidadeNominal })
                .FirstOrDefaultAsync(cancellationToken);

            var ultimaLeitura = sessao is null ? null : await _context.Producoes
                .AsNoTracking()
                .Where(p => p.SessaoId == sessao.Id)
                .OrderByDescending(p => p.Hora)
                .Select(p => new { p.Quantidade, p.Refugo, p.Hora })
                .FirstOrDefaultAsync(cancellationToken);

            var parada = sessao is null ? null : await _context.Paradas
                .AsNoTracking()
                .Where(p => p.SessaoId == sessao.Id && p.Fim == null)
                .OrderByDescending(p => p.Inicio)
                .Select(p => new { p.Id, p.Inicio, p.MotivoId, Motivo = p.Motivo == null ? null : p.Motivo.Nome, Tipo = p.Motivo == null ? (TipoParada?)null : p.Motivo.Tipo })
                .FirstOrDefaultAsync(cancellationToken);

            var naoClassificadas = sessao is null ? 0 : await _context.Paradas
                .CountAsync(p => p.SessaoId == sessao.Id && p.MotivoId == null, cancellationToken);

            var semComunicacaoDesde = await _context.PeriodosSemComunicacao
                .Where(p => p.MaquinaLinhaId == a.MaquinaLinhaId && p.Fim == null)
                .OrderBy(p => p.Inicio)
                .Select(p => (DateTime?)p.Inicio)
                .FirstOrDefaultAsync(cancellationToken);

            resumos.Add(new ColetaIotResumoDto(
                a.Id, a.MaquinaLinhaId, a.Maquina, a.LinhaId, a.Linha, a.Cliente,
                a.UsuarioId, a.Usuario, a.IniciadoEm, a.TempoDeteccaoParadaSegundos,
                a.Canais.Select(c => new CanalMedicaoRequest(c.Canal, c.Multiplicador)).ToList(),
                ip,
                sessao?.Id, sessao?.Inicio, sessao?.VelocidadeNominal ?? 0,
                ultimaLeitura?.Quantidade ?? 0, ultimaLeitura?.Refugo ?? 0, ultimaLeitura?.Hora,
                parada is null ? null : new ParadaAbertaDto(parada.Id, parada.Inicio, parada.MotivoId, parada.Motivo, parada.Tipo ?? TipoParada.Interna),
                semComunicacaoDesde,
                naoClassificadas,
                a.MaquinaId));
        }

        return resumos;
    }

    // ── Configuração ────────────────────────────────────────────────

    public async Task<ConfiguracaoColetaIot> CarregarConfiguracaoAsync(
        Guid acompanhamentoId,
        CancellationToken cancellationToken = default)
    {
        var acompanhamento = await _context.Acompanhamentos
            .Include(a => a.MaquinaLinha)
            .Include(a => a.Canais)
            .FirstOrDefaultAsync(a => a.Id == acompanhamentoId, cancellationToken)
            ?? throw new InvalidOperationException($"Acompanhamento {acompanhamentoId} não existe.");

        return await MontarConfiguracaoAsync(
            acompanhamento.MaquinaLinha,
            acompanhamento.Canais.Select(c => new CanalMedicao(c.Canal, c.Multiplicador)),
            acompanhamento.TempoDeteccaoParadaSegundos,
            cancellationToken);
    }

    private async Task<ConfiguracaoColetaIot> MontarConfiguracaoAsync(
        MaquinaLinha maquinaLinha,
        IEnumerable<CanalMedicao> canais,
        int tempoDeteccaoParadaSegundos,
        CancellationToken cancellationToken)
    {
        var regras = await CarregarRegrasVigentesAsync(maquinaLinha, cancellationToken);
        return new ConfiguracaoColetaIot(
            canais,
            TimeSpan.FromSeconds(tempoDeteccaoParadaSegundos),
            _opcoes.TempoSemComunicacao,
            MapeadorRegras.ParaDominio(regras));
    }

    /// <summary>
    /// Customizadas da máquina da linha, se houver; senão, as do catálogo
    /// (criando o padrão na hora se a máquina ainda não tiver).
    /// </summary>
    private async Task<List<RegraClassificacao>> CarregarRegrasVigentesAsync(
        MaquinaLinha maquinaLinha,
        CancellationToken cancellationToken)
    {
        IQueryable<RegraClassificacao> Regras() => _context.RegrasClassificacao
            .Include(r => r.Condicoes)
            .Include(r => r.MotivoParada);

        if (await _context.ConjuntosRegras.AnyAsync(c => c.MaquinaLinhaId == maquinaLinha.Id, cancellationToken))
        {
            return await Regras()
                .Where(r => r.ConjuntoRegras.MaquinaLinhaId == maquinaLinha.Id)
                .ToListAsync(cancellationToken);
        }

        await _regrasPadrao.GarantirAsync(maquinaLinha.MaquinaId, cancellationToken);
        return await Regras()
            .Where(r => r.ConjuntoRegras.MaquinaId == maquinaLinha.MaquinaId)
            .ToListAsync(cancellationToken);
    }

    // ── Apoio ───────────────────────────────────────────────────────

    /// <summary>
    /// Sessão da coleta, com a leitura inicial de produção zerada: a produção
    /// gravada depois é o acumulado em garrafas desde o início da sessão, e o
    /// OEE continua calculando última leitura menos a inicial, como no Manual.
    /// </summary>
    internal static Sessao NovaSessao(Acompanhamento acompanhamento, DateTime inicio, decimal velocidadeNominal, decimal sobreVelocidade)
    {
        var sessao = new Sessao
        {
            Id = Guid.NewGuid(),
            MaquinaLinhaId = acompanhamento.MaquinaLinhaId,
            UsuarioId = acompanhamento.UsuarioId,
            AcompanhamentoId = acompanhamento.Id,
            Inicio = inicio,
            Status = StatusSessao.EmAndamento,
            TipoColeta = TipoColeta.SemiAutomatico,
            VelocidadeNominal = velocidadeNominal,
            SobreVelocidade = sobreVelocidade,
            CriadoEm = inicio
        };
        sessao.Producoes.Add(new Producao { Id = Guid.NewGuid(), Quantidade = 0, Refugo = 0, Hora = inicio });
        return sessao;
    }
}
