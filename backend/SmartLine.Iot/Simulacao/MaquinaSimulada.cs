using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SmartLine.Iot.Simulacao;

/// <summary>O que a máquina simulada está fazendo.</summary>
public enum CenarioSimulado
{
    /// <summary>Produzindo no ritmo configurado, sensores normais.</summary>
    Rodando,

    /// <summary>Parada; S8 sem presença (falta de garrafas na entrada).</summary>
    FaltaGarrafas,

    /// <summary>Parada; S1 sem presença (abaixo do acúmulo mínimo).</summary>
    AbaixoAcumuloMinimo,

    /// <summary>Parada; S7 com presença contínua (saída de garrafas bloqueada).</summary>
    SaidaGarrafasBloqueada,

    /// <summary>Parada; S4 com presença contínua (saída de caixas/pallets bloqueada).</summary>
    SaidaCaixasBloqueada,

    /// <summary>Parada com todos os sensores normais: vira parada não classificada.</summary>
    ParadaSemCausa,

    /// <summary>WISE desligado ou fora da rede: não publica nada.</summary>
    Desligado
}

/// <summary>Falha de comunicação simulada, por cima do que a máquina está fazendo.</summary>
public enum FalhaComunicacao
{
    /// <summary>Rede caiu: o WISE continua contando, só não publica (na volta, o contador andou).</summary>
    SemRede,

    /// <summary>WISE sem energia: não conta nada e, ao voltar, reinicia com os contadores em zero.</summary>
    SemEnergia
}

/// <summary>
/// Uma máquina com WISE, simulada: guarda contadores e sensores e gera a
/// mensagem MQTT no mesmo formato do WISE-4051 real (ver FormatoWise).
/// </summary>
/// <remarks>
/// Dirigida pelo tempo, não pelo número de mensagens: publicar mais ou menos
/// vezes não muda o que a máquina produziu, só a resolução com que se observa.
/// Guarda o tempo rodando em ticks (inteiro, exato) e calcula os pulsos em
/// decimal na hora de ler, para não acumular erro de arredondamento.
/// Sensores com a mesma ligação invertida da fábrica: 1 = sem presença.
/// </remarks>
public sealed class MaquinaSimulada
{
    private const decimal MaximoContador = 4_294_967_296m; // 2^32: o contador do WISE vira aqui

    private readonly decimal _pulsosPorMinuto;
    private readonly decimal _percentualRejeito;
    private long _ticksRodando; // tempo total produzindo desde o último reinício do WISE
    private DateTime _ultimoAvancoUtc;
    private int _sequencia;

    /// <param name="numero">Identificação (1, 2, 3...), usada no ClientId e no tópico.</param>
    /// <param name="pulsosPorMinuto">Ritmo do contador de produção (S2) rodando.</param>
    /// <param name="percentualRejeito">Pulsos no S3 como percentual da produção.</param>
    /// <param name="inicioUtc">Instante inicial da simulação.</param>
    public MaquinaSimulada(int numero, double pulsosPorMinuto, double percentualRejeito, DateTime inicioUtc)
    {
        if (pulsosPorMinuto <= 0) throw new ArgumentOutOfRangeException(nameof(pulsosPorMinuto));
        if (percentualRejeito is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(percentualRejeito));

        Numero = numero;
        _pulsosPorMinuto = (decimal)pulsosPorMinuto;
        _percentualRejeito = (decimal)percentualRejeito;
        _ultimoAvancoUtc = inicioUtc;
    }

    public int Numero { get; }

    public CenarioSimulado Cenario { get; private set; } = CenarioSimulado.Rodando;

    /// <summary>Falha de comunicação em curso; nula = comunicando.</summary>
    public FalhaComunicacao? Falha { get; private set; }

    /// <summary>Publica? Desligado ou com falha de comunicação não publica.</summary>
    public bool Publica => Cenario != CenarioSimulado.Desligado && Falha is null;

    /// <summary>Começa uma falha de comunicação (a máquina segue no cenário em que está).</summary>
    public void PerderComunicacao(FalhaComunicacao falha, DateTime agoraUtc)
    {
        Avancar(agoraUtc);
        Falha = falha;
    }

    /// <summary>Volta a comunicar. Depois de sem energia, o WISE reiniciou: contadores em zero.</summary>
    public void RecuperarComunicacao(DateTime agoraUtc)
    {
        Avancar(agoraUtc);
        if (Falha == FalhaComunicacao.SemEnergia)
            _ticksRodando = 0;
        Falha = null;
    }

    /// <summary>Valor atual do contador de produção (S2).</summary>
    public uint ContadorProducao => Contar(_pulsosPorMinuto);

    /// <summary>Valor atual do contador de rejeito (S3).</summary>
    public uint ContadorRejeito => Contar(_pulsosPorMinuto * _percentualRejeito / 100);

    /// <summary>Troca o cenário, contabilizando antes o que foi produzido até agora.</summary>
    public void MudarCenario(CenarioSimulado cenario, DateTime agoraUtc)
    {
        Avancar(agoraUtc);
        Cenario = cenario;
    }

    /// <summary>Simula o WISE reiniciando: contadores voltam a zero.</summary>
    public void Reiniciar(DateTime agoraUtc)
    {
        Avancar(agoraUtc);
        _ticksRodando = 0;
    }

    /// <summary>Produz o que o tempo decorrido manda, se estiver rodando.</summary>
    public void Avancar(DateTime agoraUtc)
    {
        if (agoraUtc <= _ultimoAvancoUtc)
            return;

        // Sem energia o WISE não conta; sem rede ele continua contando.
        if (Cenario == CenarioSimulado.Rodando && Falha != FalhaComunicacao.SemEnergia)
            _ticksRodando += (agoraUtc - _ultimoAvancoUtc).Ticks;
        _ultimoAvancoUtc = agoraUtc;
    }

    /// <summary>
    /// Mensagem do WISE neste instante:
    /// <c>{"s":1,"t":"...","q":192,"c":N,"di1":..,...,"di8":..}</c>.
    /// </summary>
    public string GerarPayload(DateTime agoraUtc)
    {
        Avancar(agoraUtc);
        _sequencia++;

        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteNumber("s", 1);
            json.WriteString("t", agoraUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            json.WriteNumber("q", 192);
            json.WriteNumber("c", _sequencia);
            // Invertidos: 1 = sem presença. Normal: S1/S8 com presença (false), S4/S7 sem acúmulo (true).
            json.WriteBoolean("di1", Cenario == CenarioSimulado.AbaixoAcumuloMinimo);
            json.WriteNumber("di2", ContadorProducao);
            json.WriteNumber("di3", ContadorRejeito);
            json.WriteBoolean("di4", Cenario != CenarioSimulado.SaidaCaixasBloqueada);
            json.WriteNumber("di5", 0);
            json.WriteNumber("di6", 0);
            json.WriteBoolean("di7", Cenario != CenarioSimulado.SaidaGarrafasBloqueada);
            json.WriteBoolean("di8", Cenario == CenarioSimulado.FaltaGarrafas);
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Pulsos no ritmo dado pelo tempo rodando, com a virada em 2^32 do contador real.</summary>
    private uint Contar(decimal pulsosPorMinuto) =>
        (uint)(Math.Floor(_ticksRodando * pulsosPorMinuto / TimeSpan.TicksPerMinute) % MaximoContador);
}
