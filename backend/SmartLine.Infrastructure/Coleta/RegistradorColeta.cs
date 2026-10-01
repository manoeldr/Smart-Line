using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Coleta;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Coleta;

/// <summary>
/// Paradas, comunicação, produção consolidada e virada do dia.
/// Em <see cref="RegistrarAsync"/>, a <see cref="ProducaoApurada"/> normal é ignorada:
/// o serviço de coleta soma em memória e grava por <see cref="ConsolidarProducaoAsync"/>.
/// A feita sem comunicação (apurada na volta) é gravada já, numa leitura marcada.
/// <see cref="ContadorReiniciado"/> é só diagnóstico.
/// </summary>
public class RegistradorColeta : IRegistradorColeta
{
    private readonly SmartLineDbContext _context;

    public RegistradorColeta(SmartLineDbContext context)
    {
        _context = context;
    }

    public async Task RegistrarAsync(
        Guid acompanhamentoId,
        IReadOnlyList<EventoColeta> eventos,
        CancellationToken cancellationToken = default)
    {
        if (!eventos.Any(Relevante))
            return;

        // Produção feita sem comunicação: gravada já na volta, numa leitura própria.
        var comProducao = eventos.Any(e => e is ProducaoApurada { SemComunicacao: true });
        IQueryable<Sessao> consulta = _context.Sessoes.Include(s => s.Paradas);
        if (comProducao)
            consulta = consulta.Include(s => s.Producoes);
        var sessao = await consulta
            .FirstOrDefaultAsync(s => s.AcompanhamentoId == acompanhamentoId
                                      && s.Status == StatusSessao.EmAndamento, cancellationToken);
        if (sessao is null)
            return; // finalizado entre a mensagem e a gravação: nada a registrar

        foreach (var evento in eventos)
        {
            switch (evento)
            {
                case ParadaIniciada e:
                    AbrirParada(sessao, e.InstanteUtc, e.Classificacao);
                    break;

                case ParadaReclassificada e:
                    // Mesma parada física: ganha o motivo, sem abrir outra (na linha do tempo, uma
                    // parada nunca vem colada noutra sem marcha no meio).
                    ClassificarParadaAberta(sessao, e.InstanteUtc, e.Classificacao);
                    break;

                case ParadaEncerrada e:
                    FecharParadaAberta(sessao, e.InstanteUtc);
                    break;

                case ComunicacaoPerdida e:
                    await AbrirPeriodoSemComunicacaoAsync(sessao.MaquinaLinhaId, e.InstanteUtc, cancellationToken);
                    break;

                case ComunicacaoRestabelecida e:
                    await FecharPeriodoSemComunicacaoAsync(sessao.MaquinaLinhaId, e.InstanteUtc, e.ProducaoNaoRecuperada, cancellationToken);
                    break;

                case ProducaoApurada { SemComunicacao: true } p:
                    // À parte da produção normal (que segue no pendente até a consolidação): o
                    // gráfico mostra em cinza e o OEE não conta.
                    GravarLeitura(sessao, p.InstanteUtc, new ProducaoPendente(p.Garrafas, p.Rejeito), semComunicacao: true);
                    break;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    // ── Produção ────────────────────────────────────────────────────

    public async Task ConsolidarProducaoAsync(
        Guid acompanhamentoId,
        DateTime instanteUtc,
        ProducaoPendente pendente,
        IReadOnlyDictionary<CanalWise, uint>? contadoresBrutos,
        CancellationToken cancellationToken = default)
    {
        var sessao = await SessaoAbertaComProducaoAsync(acompanhamentoId, cancellationToken);
        if (sessao is null)
            return;

        GravarLeitura(sessao, instanteUtc, pendente);
        await GuardarContadoresAsync(acompanhamentoId, instanteUtc, contadoresBrutos, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // ── Virada do dia ───────────────────────────────────────────────

    public async Task VirarDiaAsync(
        Guid acompanhamentoId,
        DateTime viradaUtc,
        ProducaoPendente pendente,
        IReadOnlyDictionary<CanalWise, uint>? contadoresBrutos,
        CancellationToken cancellationToken = default)
    {
        var sessao = await SessaoAbertaComProducaoAsync(acompanhamentoId, cancellationToken, comParadas: true);
        if (sessao is null || sessao.Inicio >= viradaUtc)
            return; // finalizado, ou esta virada já foi feita

        // 1. O que foi produzido até aqui fica no dia que termina.
        GravarLeitura(sessao, viradaUtc, pendente);
        await GuardarContadoresAsync(acompanhamentoId, viradaUtc, contadoresBrutos, cancellationToken);

        // 2. Fecha o dia.
        var paradaEmCurso = sessao.Paradas.FirstOrDefault(p => p.Fim is null);
        FecharParadaAberta(sessao, viradaUtc);
        sessao.Fim = viradaUtc;
        sessao.Status = StatusSessao.Finalizada;
        sessao.MotivoFechamento = MotivoFechamentoSessao.ViradaDoDia;

        // 3. Abre o novo dia, com os mesmos dados de quem iniciou a coleta. O contador segue
        //    de onde o dia anterior parou (a leitura inicial do novo dia é a final do anterior),
        //    como no Manual, onde a leitura inicial é o contador da máquina.
        var fechamento = sessao.Producoes.OrderByDescending(p => p.Hora).FirstOrDefault();
        var nova = new Sessao
        {
            Id = Guid.NewGuid(),
            MaquinaLinhaId = sessao.MaquinaLinhaId,
            UsuarioId = sessao.UsuarioId,
            AcompanhamentoId = acompanhamentoId,
            Inicio = viradaUtc,
            Status = StatusSessao.EmAndamento,
            TipoColeta = sessao.TipoColeta,
            VelocidadeNominal = sessao.VelocidadeNominal,
            SobreVelocidade = sessao.SobreVelocidade,
            CriadoEm = viradaUtc
        };
        nova.Producoes.Add(new Producao { Id = Guid.NewGuid(), Quantidade = fechamento?.Quantidade ?? 0, Refugo = fechamento?.Refugo ?? 0, Hora = viradaUtc });
        _context.Sessoes.Add(nova);

        // 4. A parada em curso continua no novo dia, com a mesma classificação — inclusive
        //    a que alguém já tenha dado à mão, que vale para a parada física inteira.
        if (paradaEmCurso is not null)
        {
            var continuacao = AbrirParada(nova, viradaUtc,
                new ClassificacaoParada(TipoParada.Interna, paradaEmCurso.MotivoId, paradaEmCurso.RegraClassificacaoId),
                historicoDoSistema: false);

            // O histórico do novo trecho repete a última classificação, com o mesmo autor
            // (o sistema ou quem classificou à mão), para cada trecho ter o seu registro.
            var ultimaClassificacao = await _context.HistoricosClassificacaoParada
                .Where(h => h.ParadaId == paradaEmCurso.Id)
                .OrderByDescending(h => h.AlteradoEm)
                .FirstOrDefaultAsync(cancellationToken);
            if (ultimaClassificacao is not null)
            {
                _context.HistoricosClassificacaoParada.Add(new HistoricoClassificacaoParada
                {
                    Id = Guid.NewGuid(),
                    ParadaId = continuacao.Id,
                    MotivoAnteriorId = null,
                    MotivoNovoId = ultimaClassificacao.MotivoNovoId,
                    UsuarioId = ultimaClassificacao.UsuarioId,
                    AlteradoEm = viradaUtc
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    // ── Apoio ───────────────────────────────────────────────────────

    private async Task<Sessao?> SessaoAbertaComProducaoAsync(Guid acompanhamentoId, CancellationToken cancellationToken, bool comParadas = false)
    {
        IQueryable<Sessao> q = _context.Sessoes.Include(s => s.Producoes);
        if (comParadas)
            q = q.Include(s => s.Paradas);
        return await q.FirstOrDefaultAsync(s => s.AcompanhamentoId == acompanhamentoId
                                                && s.Status == StatusSessao.EmAndamento, cancellationToken);
    }

    /// <summary>
    /// Nova leitura com o total da sessão (última + pendente), como o Manual
    /// grava o valor do contador. O OEE faz última − inicial.
    /// </summary>
    private void GravarLeitura(Sessao sessao, DateTime instante, ProducaoPendente pendente, bool semComunicacao = false)
    {
        if (pendente.Vazia)
            return;

        var ultima = sessao.Producoes.OrderByDescending(p => p.Hora).FirstOrDefault();
        var leitura = new Producao
        {
            Id = Guid.NewGuid(),
            SessaoId = sessao.Id,
            Quantidade = Somar(ultima?.Quantidade ?? 0, pendente.Garrafas),
            Refugo = Somar(ultima?.Refugo ?? 0, pendente.Rejeito),
            // Duas gravações no mesmo instante (ex.: consolidação e virada juntas) somariam
            // leituras com a mesma hora; empurra 1 ms para manter a ordem sem ambiguidade.
            Hora = ultima is not null && instante <= ultima.Hora ? ultima.Hora.AddMilliseconds(1) : instante,
            SemComunicacao = semComunicacao
        };
        sessao.Producoes.Add(leitura);
        _context.Producoes.Add(leitura);
    }

    /// <summary>A coluna é int; um dia de garrafas cabe com folga, mas não deixa estourar em silêncio.</summary>
    private static int Somar(int atual, long incremento) =>
        (int)Math.Min(int.MaxValue, atual + Math.Max(0, incremento));

    private async Task GuardarContadoresAsync(
        Guid acompanhamentoId,
        DateTime instante,
        IReadOnlyDictionary<CanalWise, uint>? contadoresBrutos,
        CancellationToken cancellationToken)
    {
        if (contadoresBrutos is null || contadoresBrutos.Count == 0)
            return;

        var canais = await _context.AcompanhamentoCanais
            .Where(c => c.AcompanhamentoId == acompanhamentoId)
            .ToListAsync(cancellationToken);
        foreach (var canal in canais)
        {
            if (contadoresBrutos.TryGetValue(canal.Canal, out var valor))
            {
                canal.UltimoValorBruto = valor;
                canal.UltimoValorEm = instante;
            }
        }
    }

    private static bool Relevante(EventoColeta e) =>
        e is ParadaIniciada or ParadaReclassificada or ParadaEncerrada
            or ComunicacaoPerdida or ComunicacaoRestabelecida
            or ProducaoApurada { SemComunicacao: true };

    private Parada AbrirParada(Sessao sessao, DateTime instante, ClassificacaoParada classificacao, bool historicoDoSistema = true)
    {
        // Nunca duas abertas: se sobrou uma (evento perdido), fecha antes.
        FecharParadaAberta(sessao, instante);

        // O início é retroativo ao último incremento; não pode cair antes da sessão
        // (ex.: coleta retomada logo depois de reiniciar).
        var inicio = instante < sessao.Inicio ? sessao.Inicio : instante;

        var parada = new Parada
        {
            Id = Guid.NewGuid(),
            SessaoId = sessao.Id,
            Inicio = inicio,
            MotivoId = classificacao.MotivoParadaId,
            RegraClassificacaoId = classificacao.RegraId
        };
        sessao.Paradas.Add(parada);
        _context.Paradas.Add(parada);

        // Primeiro registro do histórico = o que o sensor disse (usuário nulo). Parada não
        // classificada não tem o que registrar; o histórico começa quando alguém classificar.
        if (historicoDoSistema && !classificacao.EhNaoClassificada)
        {
            _context.HistoricosClassificacaoParada.Add(new HistoricoClassificacaoParada
            {
                Id = Guid.NewGuid(),
                ParadaId = parada.Id,
                MotivoAnteriorId = null,
                MotivoNovoId = classificacao.MotivoParadaId,
                UsuarioId = null,
                AlteradoEm = inicio
            });
        }

        return parada;
    }

    /// <summary>
    /// Dá à parada aberta a causa que o sensor identificou, se ela ainda está sem
    /// motivo. Se já tem (a primeira causa, ou alguém classificou à mão), fica como está.
    /// </summary>
    private void ClassificarParadaAberta(Sessao sessao, DateTime instante, ClassificacaoParada classificacao)
    {
        var aberta = sessao.Paradas.FirstOrDefault(p => p.Fim is null);
        if (aberta is null)
        {
            // Evento de início perdido (não deveria acontecer): registra a parada a partir daqui.
            AbrirParada(sessao, instante, classificacao);
            return;
        }

        if (aberta.MotivoId is not null || classificacao.EhNaoClassificada)
            return;

        aberta.MotivoId = classificacao.MotivoParadaId;
        aberta.RegraClassificacaoId = classificacao.RegraId;
        _context.HistoricosClassificacaoParada.Add(new HistoricoClassificacaoParada
        {
            Id = Guid.NewGuid(),
            ParadaId = aberta.Id,
            MotivoAnteriorId = null,
            MotivoNovoId = classificacao.MotivoParadaId,
            UsuarioId = null,
            AlteradoEm = instante < aberta.Inicio ? aberta.Inicio : instante
        });
    }

    private static void FecharParadaAberta(Sessao sessao, DateTime instante)
    {
        foreach (var aberta in sessao.Paradas.Where(p => p.Fim is null))
            aberta.Fim = instante < aberta.Inicio ? aberta.Inicio : instante;
    }

    private async Task AbrirPeriodoSemComunicacaoAsync(Guid maquinaLinhaId, DateTime inicio, CancellationToken cancellationToken)
    {
        if (await PeriodoAbertoAsync(maquinaLinhaId, cancellationToken) is not null)
            return;

        _context.PeriodosSemComunicacao.Add(new PeriodoSemComunicacao
        {
            Id = Guid.NewGuid(),
            MaquinaLinhaId = maquinaLinhaId,
            Inicio = inicio
        });
    }

    private async Task FecharPeriodoSemComunicacaoAsync(Guid maquinaLinhaId, DateTime fim, bool producaoNaoRecuperada, CancellationToken cancellationToken)
    {
        var aberto = await PeriodoAbertoAsync(maquinaLinhaId, cancellationToken);
        if (aberto is null)
            return;
        aberto.Fim = fim < aberto.Inicio ? aberto.Inicio : fim;
        aberto.ProducaoNaoRecuperada |= producaoNaoRecuperada;
    }

    /// <summary>
    /// Período aberto considerando também as mudanças desta mesma gravação, que
    /// ainda não estão no banco: um período adicionado agora conta como aberto, e
    /// um fechado agora (mas ainda aberto no banco) não conta. Por isso o filtro
    /// final é na memória, depois da consulta.
    /// </summary>
    private async Task<PeriodoSemComunicacao?> PeriodoAbertoAsync(Guid maquinaLinhaId, CancellationToken cancellationToken)
    {
        var local = _context.PeriodosSemComunicacao.Local
            .FirstOrDefault(p => p.MaquinaLinhaId == maquinaLinhaId && p.Fim is null);
        if (local is not null)
            return local;

        var abertosNoBanco = await _context.PeriodosSemComunicacao
            .Where(p => p.MaquinaLinhaId == maquinaLinhaId && p.Fim == null)
            .ToListAsync(cancellationToken);
        return abertosNoBanco.FirstOrDefault(p => p.Fim is null);
    }
}
