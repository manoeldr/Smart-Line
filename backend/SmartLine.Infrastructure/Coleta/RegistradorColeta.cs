using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Coleta;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Data;

namespace SmartLine.Infrastructure.Coleta;

/// <summary>
/// Paradas e comunicação. A produção (<see cref="ProducaoApurada"/>) é
/// acumulada em memória e consolidada a cada intervalo pelo serviço de coleta,
/// então aqui é ignorada; <see cref="ContadorReiniciado"/> é só diagnóstico.
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

        var sessao = await _context.Sessoes
            .Include(s => s.Paradas)
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
                    // Mesma parada física, causa nova: fecha um trecho e abre outro no mesmo instante.
                    FecharParadaAberta(sessao, e.InstanteUtc);
                    AbrirParada(sessao, e.InstanteUtc, e.Classificacao);
                    break;

                case ParadaEncerrada e:
                    FecharParadaAberta(sessao, e.InstanteUtc);
                    break;

                case ComunicacaoPerdida e:
                    await AbrirPeriodoSemComunicacaoAsync(sessao.MaquinaLinhaId, e.InstanteUtc, cancellationToken);
                    break;

                case ComunicacaoRestabelecida e:
                    await FecharPeriodoSemComunicacaoAsync(sessao.MaquinaLinhaId, e.InstanteUtc, cancellationToken);
                    break;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static bool Relevante(EventoColeta e) =>
        e is ParadaIniciada or ParadaReclassificada or ParadaEncerrada
            or ComunicacaoPerdida or ComunicacaoRestabelecida;

    private void AbrirParada(Sessao sessao, DateTime instante, ClassificacaoParada classificacao)
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
        if (!classificacao.EhNaoClassificada)
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

    private async Task FecharPeriodoSemComunicacaoAsync(Guid maquinaLinhaId, DateTime fim, CancellationToken cancellationToken)
    {
        var aberto = await PeriodoAbertoAsync(maquinaLinhaId, cancellationToken);
        if (aberto is not null)
            aberto.Fim = fim < aberto.Inicio ? aberto.Inicio : fim;
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
