using SmartLine.Core.Coleta;
using SmartLine.Core.Iot;
using static SmartLine.Tests.Iot.Cenario;

namespace SmartLine.Tests.Iot;

/// <summary>Coleta iniciada e WISE que não aparece.</summary>
public class EstadoMaquinaIotInicioColetaTests
{
    [Fact]
    public void SemInicioInformado_EsperaAPrimeiraMensagemIndefinidamente()
    {
        var estado = new EstadoMaquinaIot(Config());

        Assert.Empty(estado.Verificar(Em(3600)));
        Assert.Equal(SituacaoMaquina.AguardandoPrimeiraAmostra, estado.Situacao);
    }

    [Fact]
    public void NenhumaMensagem_PerdeAComunicacaoDesdeOInicio_EVoltaNaPrimeira()
    {
        var estado = new EstadoMaquinaIot(Config(), inicioColetaUtc: T0);

        Assert.Empty(estado.Verificar(Em(90)));
        Assert.Equal(new EventoColeta[] { new ComunicacaoPerdida(T0) }, estado.Verificar(Em(91)));
        Assert.Equal(SituacaoMaquina.SemComunicacao, estado.Situacao);

        var eventos = estado.Processar(Amostra(120, s2: 500));

        Assert.Equal(new EventoColeta[] { new ComunicacaoRestabelecida(Em(120)) }, eventos); // 500 é referência, não produção
        Assert.Equal(SituacaoMaquina.Rodando, estado.Situacao);
    }

    [Fact]
    public void PrimeiraMensagemAtrasada_SemVerificacaoNoMeio_RegistraOPeriodoMesmoAssim()
    {
        var estado = new EstadoMaquinaIot(Config(), inicioColetaUtc: T0);

        var eventos = estado.Processar(Amostra(200, s2: 5));

        Assert.Equal(new EventoColeta[] { new ComunicacaoPerdida(T0), new ComunicacaoRestabelecida(Em(200)) }, eventos);
    }

    [Fact]
    public void PrimeiraMensagemNoPrazo_NaoTemPeriodoSemComunicacao()
    {
        var estado = new EstadoMaquinaIot(Config(), inicioColetaUtc: T0);

        Assert.Empty(estado.Processar(Amostra(60, s2: 5)));
        Assert.Equal(SituacaoMaquina.Rodando, estado.Situacao);
    }

    [Fact]
    public void InicioForaDeUtc_Recusa() =>
        Assert.Throws<ArgumentException>(() =>
            new EstadoMaquinaIot(Config(), inicioColetaUtc: DateTime.SpecifyKind(T0, DateTimeKind.Local)));
}
