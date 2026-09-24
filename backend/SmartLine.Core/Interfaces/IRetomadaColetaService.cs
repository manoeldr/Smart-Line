using SmartLine.Core.Iot;

namespace SmartLine.Core.Interfaces;

/// <summary>
/// Retoma as coletas em andamento quando o backend sobe (reinício, queda de
/// energia, atualização). Deixa o banco coerente e devolve o que o serviço de
/// coleta precisa para recriar o estado de cada máquina.
/// </summary>
public interface IRetomadaColetaService
{
    /// <summary>
    /// Para cada acompanhamento em andamento:
    /// <list type="number">
    /// <item>fecha a parada aberta na <b>última evidência</b> (último dado
    /// recebido antes da queda) — depois disso não se sabe o que aconteceu;</item>
    /// <item>registra o tempo fora do ar como período sem comunicação, a partir
    /// da mesma evidência (não é parada da máquina);</item>
    /// <item>faz as viradas de meia-noite que ficaram para trás;</item>
    /// <item>carrega configuração e últimos contadores brutos, para a primeira
    /// mensagem do WISE já creditar o que foi produzido no intervalo.</item>
    /// </list>
    /// Idempotente. Um acompanhamento com problema não impede os outros.
    /// </summary>
    Task<IReadOnlyList<ColetaRetomada>> RetomarAsync(CancellationToken cancellationToken = default);
}

/// <summary>Tudo para recriar o estado de uma máquina.</summary>
/// <param name="Configuracao">Nula quando <paramref name="Erro"/> está preenchido.</param>
/// <param name="SemComunicacaoDesdeUtc">Início do período sem comunicação em aberto.</param>
public sealed record ColetaRetomada(
    Guid AcompanhamentoId,
    Guid MaquinaLinhaId,
    ConfiguracaoColetaIot? Configuracao,
    IReadOnlyDictionary<CanalWise, uint> Contadores,
    DateTime SemComunicacaoDesdeUtc,
    string? Erro)
{
    /// <summary>Estado pronto para receber a próxima mensagem do WISE.</summary>
    /// <exception cref="InvalidOperationException">Retomada com erro.</exception>
    public EstadoMaquinaIot CriarEstado() =>
        Configuracao is null
            ? throw new InvalidOperationException($"Acompanhamento {AcompanhamentoId} não pôde ser retomado: {Erro}")
            : new EstadoMaquinaIot(Configuracao, Contadores, SemComunicacaoDesdeUtc);
}
