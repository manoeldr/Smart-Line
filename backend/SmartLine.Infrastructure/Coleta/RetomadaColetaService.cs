using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Coleta;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Coleta;

public class RetomadaColetaService : IRetomadaColetaService
{
    private readonly SmartLineDbContext _context;
    private readonly IRegistradorColeta _registrador;
    private readonly IAcompanhamentoService _acompanhamentos;
    private readonly TimeProvider _tempo;
    private readonly OpcoesColetaIot _opcoes;

    public RetomadaColetaService(
        SmartLineDbContext context,
        IRegistradorColeta registrador,
        IAcompanhamentoService acompanhamentos,
        TimeProvider tempo,
        OpcoesColetaIot opcoes)
    {
        _context = context;
        _registrador = registrador;
        _acompanhamentos = acompanhamentos;
        _tempo = tempo;
        _opcoes = opcoes;
    }

    public async Task<IReadOnlyList<ColetaRetomada>> RetomarAsync(CancellationToken cancellationToken = default)
    {
        var agora = _tempo.GetUtcNow().UtcDateTime;
        var emAndamento = await _context.Acompanhamentos
            .AsNoTracking()
            .Where(a => a.FinalizadoEm == null)
            .Select(a => new { a.Id, a.MaquinaLinhaId, a.IniciadoEm })
            .ToListAsync(cancellationToken);

        var retomadas = new List<ColetaRetomada>();
        foreach (var a in emAndamento)
        {
            // Cada gravação usa o contexto já rastreando o que as anteriores carregaram;
            // limpar entre acompanhamentos evita misturar estado de um com o outro.
            _context.ChangeTracker.Clear();
            retomadas.Add(await RetomarAsync(a.Id, a.MaquinaLinhaId, a.IniciadoEm, agora, cancellationToken));
        }

        return retomadas;
    }

    private async Task<ColetaRetomada> RetomarAsync(
        Guid acompanhamentoId,
        Guid maquinaLinhaId,
        DateTime iniciadoEm,
        DateTime agora,
        CancellationToken cancellationToken)
    {
        var canais = await _context.AcompanhamentoCanais
            .AsNoTracking()
            .Where(c => c.AcompanhamentoId == acompanhamentoId)
            .ToListAsync(cancellationToken);

        var periodoAberto = await _context.PeriodosSemComunicacao
            .AsNoTracking()
            .Where(p => p.MaquinaLinhaId == maquinaLinhaId && p.Fim == null)
            .OrderBy(p => p.Inicio)
            .FirstOrDefaultAsync(cancellationToken);

        var evidencia = await UltimaEvidenciaAsync(acompanhamentoId, maquinaLinhaId, iniciadoEm, canais, cancellationToken);

        // 1 e 2. Fecha a parada e marca o intervalo fora do ar. Se o WISE já estava sem
        // comunicação antes da queda, o período aberto continua (o registrador não duplica).
        await _registrador.RegistrarAsync(acompanhamentoId,
            [new ParadaEncerrada(evidencia), new ComunicacaoPerdida(evidencia)], cancellationToken);

        // 3. Viradas que ficaram para trás, uma por dia.
        await VirarDiasPerdidosAsync(acompanhamentoId, agora, cancellationToken);

        // 4. O que o coletor precisa.
        var contadores = canais
            .Where(c => c.UltimoValorBruto is >= 0 and <= uint.MaxValue)
            .ToDictionary(c => c.Canal, c => (uint)c.UltimoValorBruto!.Value);
        var semComunicacaoDesde = periodoAberto?.Inicio ?? evidencia;

        try
        {
            var configuracao = await _acompanhamentos.CarregarConfiguracaoAsync(acompanhamentoId, cancellationToken);
            return new ColetaRetomada(acompanhamentoId, maquinaLinhaId, configuracao, contadores, semComunicacaoDesde, null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return new ColetaRetomada(acompanhamentoId, maquinaLinhaId, null, contadores, semComunicacaoDesde, ex.Message);
        }
    }

    /// <summary>
    /// Último instante em que se sabe algo da máquina: última consolidação,
    /// última mensagem do WISE, início da parada em curso, início da sessão
    /// aberta ou, no mínimo, o início da coleta.
    /// </summary>
    private async Task<DateTime> UltimaEvidenciaAsync(
        Guid acompanhamentoId,
        Guid maquinaLinhaId,
        DateTime iniciadoEm,
        IEnumerable<Core.Entities.Tenant.AcompanhamentoCanal> canais,
        CancellationToken cancellationToken)
    {
        var candidatos = new List<DateTime> { iniciadoEm };
        candidatos.AddRange(canais.Where(c => c.UltimoValorEm is not null).Select(c => c.UltimoValorEm!.Value));

        var ultimaMensagem = await _context.Acompanhamentos
            .Where(a => a.Id == acompanhamentoId)
            .Select(a => a.UltimaMensagemWiseEm)
            .FirstOrDefaultAsync(cancellationToken);
        if (ultimaMensagem is { } m) candidatos.Add(m);

        var sessao = await _context.Sessoes
            .AsNoTracking()
            .Include(s => s.Paradas)
            .Include(s => s.Producoes)
            .FirstOrDefaultAsync(s => s.AcompanhamentoId == acompanhamentoId
                                      && s.Status == StatusSessao.EmAndamento, cancellationToken);
        if (sessao is not null)
        {
            candidatos.Add(sessao.Inicio);
            candidatos.AddRange(sessao.Paradas.Where(p => p.Fim is null).Select(p => p.Inicio));
            candidatos.AddRange(sessao.Producoes.Select(p => p.Hora));
        }

        return candidatos.Max();
    }

    private async Task VirarDiasPerdidosAsync(Guid acompanhamentoId, DateTime agora, CancellationToken cancellationToken)
    {
        // Limite de segurança: um backend desligado por mais de um ano não deve travar a subida.
        for (var i = 0; i < 400; i++)
        {
            _context.ChangeTracker.Clear();
            var inicioSessao = await _context.Sessoes
                .Where(s => s.AcompanhamentoId == acompanhamentoId && s.Status == StatusSessao.EmAndamento)
                .Select(s => (DateTime?)s.Inicio)
                .FirstOrDefaultAsync(cancellationToken);
            if (inicioSessao is null)
                return;

            var virada = CalendarioColeta.ProximaMeiaNoiteUtc(inicioSessao.Value, _opcoes.Fuso);
            if (virada > agora)
                return;

            await _registrador.VirarDiaAsync(acompanhamentoId, virada, ProducaoPendente.Nenhuma, null, cancellationToken);
        }
    }
}
