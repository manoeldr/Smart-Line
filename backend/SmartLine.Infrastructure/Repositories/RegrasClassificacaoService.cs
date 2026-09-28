using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Entities.Global;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Repositories;

public class RegrasClassificacaoService : IRegrasClassificacaoService
{
    private readonly SmartLineDbContext _context;
    private readonly IRegrasPadraoService _regrasPadrao;
    private readonly TimeProvider _tempo;

    public RegrasClassificacaoService(SmartLineDbContext context, IRegrasPadraoService regrasPadrao, TimeProvider tempo)
    {
        _context = context;
        _regrasPadrao = regrasPadrao;
        _tempo = tempo;
    }

    // ── Catálogo ────────────────────────────────────────────────────

    public async Task<ResultadoCadastro<ConjuntoRegrasDto>> DoCatalogoAsync(Guid maquinaId, CancellationToken cancellationToken = default)
    {
        var maquina = await NomeDaMaquinaAsync(maquinaId, cancellationToken);
        if (maquina is null)
            return ResultadoCadastro<ConjuntoRegrasDto>.Inexistente();

        await _regrasPadrao.GarantirAsync(maquinaId, cancellationToken);
        var conjunto = await Conjuntos().FirstOrDefaultAsync(c => c.MaquinaId == maquinaId, cancellationToken);
        return ResultadoCadastro<ConjuntoRegrasDto>.Ok(ParaDto(conjunto, maquinaId, maquina, null));
    }

    public async Task<ResultadoCadastro<ConjuntoRegrasDto>> SalvarDoCatalogoAsync(
        Guid maquinaId, SalvarRegrasRequest request, CancellationToken cancellationToken = default)
    {
        var maquina = await NomeDaMaquinaAsync(maquinaId, cancellationToken);
        if (maquina is null)
            return ResultadoCadastro<ConjuntoRegrasDto>.Inexistente();

        await _regrasPadrao.GarantirAsync(maquinaId, cancellationToken);
        var conjunto = await Conjuntos().FirstAsync(c => c.MaquinaId == maquinaId, cancellationToken);

        var erro = await AplicarAsync(conjunto, maquinaId, request, cancellationToken);
        if (erro is not null)
            return ResultadoCadastro<ConjuntoRegrasDto>.Falha(erro);

        _context.ChangeTracker.Clear();
        return await DoCatalogoAsync(maquinaId, cancellationToken);
    }

    public async Task<ResultadoCadastro<ConjuntoRegrasDto>> RestaurarPadraoAsync(Guid maquinaId, CancellationToken cancellationToken = default)
    {
        if (await NomeDaMaquinaAsync(maquinaId, cancellationToken) is null)
            return ResultadoCadastro<ConjuntoRegrasDto>.Inexistente();

        // Apagar e recriar numa transação: o padrão usa os mesmos Ids determinísticos.
        await using (var transacao = await _context.Database.BeginTransactionAsync(cancellationToken))
        {
            // Com regras e condições carregadas, o EF apaga tudo explicitamente (não depende do cascade do banco).
            var atual = await Conjuntos().FirstOrDefaultAsync(c => c.MaquinaId == maquinaId, cancellationToken);
            if (atual is not null)
            {
                _context.ConjuntosRegras.Remove(atual);
                await _context.SaveChangesAsync(cancellationToken);
                _context.ChangeTracker.Clear();
            }

            await _regrasPadrao.GarantirAsync(maquinaId, cancellationToken);
            await transacao.CommitAsync(cancellationToken);
        }

        _context.ChangeTracker.Clear();
        return await DoCatalogoAsync(maquinaId, cancellationToken);
    }

    // ── Máquina da linha ────────────────────────────────────────────

    public async Task<ResultadoCadastro<ConjuntoRegrasDto>> DaMaquinaLinhaAsync(Guid maquinaLinhaId, CancellationToken cancellationToken = default)
    {
        var ml = await _context.MaquinasLinha.AsNoTracking()
            .Where(m => m.Id == maquinaLinhaId)
            .Select(m => new { m.MaquinaId, Maquina = m.Maquina.Nome })
            .FirstOrDefaultAsync(cancellationToken);
        if (ml is null)
            return ResultadoCadastro<ConjuntoRegrasDto>.Inexistente();

        var personalizado = await Conjuntos().FirstOrDefaultAsync(c => c.MaquinaLinhaId == maquinaLinhaId, cancellationToken);
        if (personalizado is not null)
            return ResultadoCadastro<ConjuntoRegrasDto>.Ok(ParaDto(personalizado, ml.MaquinaId, ml.Maquina, maquinaLinhaId));

        return await DoCatalogoAsync(ml.MaquinaId, cancellationToken);
    }

    public async Task<ResultadoCadastro<ConjuntoRegrasDto>> SalvarDaMaquinaLinhaAsync(
        Guid maquinaLinhaId, SalvarRegrasRequest request, CancellationToken cancellationToken = default)
    {
        var maquinaId = await _context.MaquinasLinha
            .Where(m => m.Id == maquinaLinhaId)
            .Select(m => (Guid?)m.MaquinaId)
            .FirstOrDefaultAsync(cancellationToken);
        if (maquinaId is null)
            return ResultadoCadastro<ConjuntoRegrasDto>.Inexistente();

        var conjunto = await Conjuntos().FirstOrDefaultAsync(c => c.MaquinaLinhaId == maquinaLinhaId, cancellationToken);
        if (conjunto is null)
        {
            conjunto = new ConjuntoRegras
            {
                Id = Guid.NewGuid(),
                MaquinaLinhaId = maquinaLinhaId,
                CriadoEm = _tempo.GetUtcNow().UtcDateTime
            };
            _context.ConjuntosRegras.Add(conjunto);
        }

        var erro = await AplicarAsync(conjunto, maquinaId.Value, request, cancellationToken);
        if (erro is not null)
            return ResultadoCadastro<ConjuntoRegrasDto>.Falha(erro);

        _context.ChangeTracker.Clear();
        return await DaMaquinaLinhaAsync(maquinaLinhaId, cancellationToken);
    }

    public async Task<ResultadoCadastro<ConjuntoRegrasDto>> RemoverPersonalizacaoAsync(Guid maquinaLinhaId, CancellationToken cancellationToken = default)
    {
        if (!await _context.MaquinasLinha.AnyAsync(m => m.Id == maquinaLinhaId, cancellationToken))
            return ResultadoCadastro<ConjuntoRegrasDto>.Inexistente();

        var personalizado = await Conjuntos().FirstOrDefaultAsync(c => c.MaquinaLinhaId == maquinaLinhaId, cancellationToken);
        if (personalizado is not null)
        {
            _context.ConjuntosRegras.Remove(personalizado);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await DaMaquinaLinhaAsync(maquinaLinhaId, cancellationToken);
    }

    // ── Aplicar a lista ─────────────────────────────────────────────

    /// <summary>Valida tudo antes de mexer; se passar, substitui as regras do conjunto e grava.</summary>
    /// <returns>Mensagem de erro para o usuário, ou nulo se gravou.</returns>
    private async Task<string?> AplicarAsync(
        ConjuntoRegras conjunto, Guid maquinaId, SalvarRegrasRequest request, CancellationToken cancellationToken)
    {
        var motivos = await _context.MotivosParada.AsNoTracking()
            .Where(m => m.MaquinaId == maquinaId)
            .ToDictionaryAsync(m => m.Id, cancellationToken);

        var regras = request.Regras ?? [];
        for (var i = 0; i < regras.Count; i++)
        {
            var erro = Validar(regras[i], motivos);
            if (erro is not null)
                return $"Regra {i + 1}: {erro}";
        }

        var existentes = conjunto.Regras.ToDictionary(r => r.Id);
        var mantidas = new HashSet<Guid>();

        for (var i = 0; i < regras.Count; i++)
        {
            var pedido = regras[i];
            RegraClassificacao regra;
            if (pedido.Id is { } id && existentes.TryGetValue(id, out var existente) && mantidas.Add(id))
            {
                regra = existente;
                _context.CondicoesRegra.RemoveRange(regra.Condicoes);
                regra.Condicoes.Clear();
            }
            else
            {
                // Id de outro conjunto (ex.: copiando as do catálogo para personalizar) vira regra nova.
                regra = new RegraClassificacao { Id = Guid.NewGuid(), ConjuntoRegrasId = conjunto.Id };
                _context.RegrasClassificacao.Add(regra);
            }

            regra.Prioridade = i + 1;
            regra.Nome = string.IsNullOrWhiteSpace(pedido.Nome) ? motivos[pedido.MotivoParadaId].Nome : pedido.Nome.Trim();
            regra.MotivoParadaId = pedido.MotivoParadaId;
            regra.Ativa = pedido.Ativa;

            foreach (var c in pedido.Condicoes)
            {
                var ehTempo = c.Tipo == TipoCondicao.TempoParadaMinimo;
                _context.CondicoesRegra.Add(new CondicaoRegra
                {
                    Id = Guid.NewGuid(),
                    RegraClassificacaoId = regra.Id,
                    Tipo = c.Tipo,
                    Canal = ehTempo ? null : c.Canal,
                    TempoMinimoSegundos = ehTempo ? c.TempoMinimoSegundos : null
                });
            }
        }

        foreach (var removida in existentes.Values.Where(r => !mantidas.Contains(r.Id)))
            _context.RegrasClassificacao.Remove(removida);

        await _context.SaveChangesAsync(cancellationToken);
        return null;
    }

    private static string? Validar(SalvarRegraRequest regra, IReadOnlyDictionary<Guid, MotivoParada> motivos)
    {
        if (!motivos.TryGetValue(regra.MotivoParadaId, out var motivo))
            return "motivo não encontrado para esta máquina.";
        if (regra.Ativa && !motivo.Ativo)
            return $"o motivo \"{motivo.Nome}\" está inativo. Escolha outro ou desative a regra.";

        var condicoes = regra.Condicoes ?? [];
        if (condicoes.Count == 0)
            return "informe ao menos uma condição (sem condição, a regra classificaria qualquer parada).";

        foreach (var c in condicoes)
        {
            switch (c.Tipo)
            {
                case TipoCondicao.SensorEmAlarme or TipoCondicao.SensorForaDeAlarme:
                    if (c.Canal is not { } canal || !Enum.IsDefined(canal))
                        return "condição de sensor sem sensor.";
                    if (MapaWise.Definicao(canal).Tipo != TipoCanal.Estado)
                        return $"{canal} é contador; as condições usam só os sensores de estado (S1, S4, S7, S8).";
                    break;

                case TipoCondicao.TempoParadaMinimo:
                    if (c.TempoMinimoSegundos is not > 0)
                        return "condição de tempo precisa de um tempo mínimo maior que zero.";
                    break;

                default:
                    return $"tipo de condição desconhecido ({c.Tipo}).";
            }
        }

        return null;
    }

    // ── Apoio ───────────────────────────────────────────────────────

    private IQueryable<ConjuntoRegras> Conjuntos() => _context.ConjuntosRegras
        .Include(c => c.Regras).ThenInclude(r => r.Condicoes)
        .Include(c => c.Regras).ThenInclude(r => r.MotivoParada);

    private Task<string?> NomeDaMaquinaAsync(Guid maquinaId, CancellationToken cancellationToken) =>
        _context.Maquinas.Where(m => m.Id == maquinaId).Select(m => (string?)m.Nome).FirstOrDefaultAsync(cancellationToken);

    private static ConjuntoRegrasDto ParaDto(ConjuntoRegras? conjunto, Guid maquinaId, string maquina, Guid? maquinaLinhaId) =>
        new(
            conjunto?.Id,
            maquinaId,
            maquina,
            maquinaLinhaId,
            conjunto?.MaquinaLinhaId is not null,
            (conjunto?.Regras ?? [])
                .OrderBy(r => r.Prioridade).ThenBy(r => r.Nome)
                .Select(r => new RegraDto(
                    r.Id, r.Prioridade, r.Nome, r.MotivoParadaId, r.MotivoParada.Nome, r.MotivoParada.Tipo,
                    r.MotivoParada.Ativo, r.Ativa,
                    r.Condicoes
                        .OrderBy(c => c.Tipo).ThenBy(c => c.Canal)
                        .Select(c => new CondicaoDto(c.Tipo, c.Canal, c.TempoMinimoSegundos))
                        .ToList()))
                .ToList());
}
