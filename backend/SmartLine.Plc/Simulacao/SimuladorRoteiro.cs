using SmartLine.Core.Plc;

namespace SmartLine.Plc.Simulacao;

/// <summary>
/// Origem de dados determinística: devolve uma sequência de passos definida de
/// antemão, um por chamada de <see cref="LerAsync"/>.
///
/// Serve para teste automatizado, onde o valor está em saber exatamente o que
/// vai acontecer: "na 3ª leitura a máquina entra em falta de produto, na 5ª a
/// conexão cai, na 8ª volta". Para deixar o sistema rodando por horas com
/// comportamento plausível, use o simulador contínuo.
/// </summary>
public sealed class SimuladorRoteiro : IPlcDataSource
{
    private readonly IReadOnlyList<PassoSimulado> _passos;
    private readonly AoFimDoRoteiro _aoFim;

    private int _indice;
    private bool _descartado;

    /// <param name="passos">Sequência a reproduzir. Não pode ser vazia.</param>
    /// <param name="aoFim">O que fazer quando o roteiro acabar.</param>
    public SimuladorRoteiro(
        IEnumerable<PassoSimulado> passos,
        AoFimDoRoteiro aoFim = AoFimDoRoteiro.RepetirDoInicio)
    {
        ArgumentNullException.ThrowIfNull(passos);

        _passos = passos.ToArray();
        _aoFim = aoFim;

        if (_passos.Count == 0)
            throw new ArgumentException("O roteiro precisa de pelo menos um passo.", nameof(passos));
    }

    /// <summary>Quantos passos já foram consumidos. Útil para asserção em teste.</summary>
    public int PassosConsumidos { get; private set; }

    /// <inheritdoc />
    public Task<LeituraPlc> LerAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_descartado, this);
        cancellationToken.ThrowIfCancellationRequested();

        var passo = ProximoPasso();
        PassosConsumidos++;

        return passo switch
        {
            PassoSimulado.Leitura leitura =>
                Task.FromResult(leitura.Valor),

            PassoSimulado.Falha falha =>
                Task.FromException<LeituraPlc>(
                    new PlcIndisponivelException("simulador", falha.Mensagem)),

            _ => throw new InvalidOperationException(
                $"Passo não reconhecido: {passo.GetType().Name}.")
        };
    }

    private PassoSimulado ProximoPasso()
    {
        if (_indice < _passos.Count)
            return _passos[_indice++];

        return _aoFim switch
        {
            AoFimDoRoteiro.RepetirDoInicio => ReiniciarERetornar(),

            AoFimDoRoteiro.RepetirUltimo => _passos[^1],

            AoFimDoRoteiro.Falhar => new PassoSimulado.Falha(
                $"Roteiro esgotado depois de {_passos.Count} passos."),

            _ => throw new InvalidOperationException(
                $"Política de fim não reconhecida: {_aoFim}.")
        };

        PassoSimulado ReiniciarERetornar()
        {
            _indice = 0;
            return _passos[_indice++];
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _descartado = true;
        return ValueTask.CompletedTask;
    }

    /// <summary>O que acontece quando o roteiro chega ao fim.</summary>
    public enum AoFimDoRoteiro
    {
        /// <summary>Volta ao primeiro passo e repete em loop. Padrão.</summary>
        RepetirDoInicio = 0,

        /// <summary>Repete o último passo indefinidamente — congela no estado final.</summary>
        RepetirUltimo = 1,

        /// <summary>
        /// Passa a falhar. Útil para provar em teste que o coletor não pediu
        /// mais leituras do que o esperado.
        /// </summary>
        Falhar = 2
    }
}

/// <summary>Um passo do roteiro: ou a máquina responde, ou não responde.</summary>
public abstract record PassoSimulado
{
    private PassoSimulado() { }

    /// <summary>A máquina respondeu com esta leitura.</summary>
    /// <param name="Valor">O que o PLC devolveu.</param>
    public sealed record Leitura(LeituraPlc Valor) : PassoSimulado;

    /// <summary>
    /// A máquina não respondeu. Vira <see cref="PlcIndisponivelException"/>,
    /// igual ao que o <c>S7DataSource</c> lançaria com o PLC fora do ar.
    /// </summary>
    /// <param name="Mensagem">Motivo, para aparecer no log do teste.</param>
    public sealed record Falha(string Mensagem) : PassoSimulado;
}