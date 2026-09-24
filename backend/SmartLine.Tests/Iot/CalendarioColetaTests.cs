using SmartLine.Core.Coleta;

namespace SmartLine.Tests.Iot;

public class CalendarioColetaTests
{
    private static readonly TimeZoneInfo Brasilia = CalendarioColeta.FusoPadrao;

    private static DateTime Utc(int dia, int hora, int minuto = 0) => new(2026, 9, dia, hora, minuto, 0, DateTimeKind.Utc);

    [Fact]
    public void FusoPadrao_EhUtcMenos3()
    {
        Assert.Equal(TimeSpan.FromHours(-3), Brasilia.GetUtcOffset(Utc(24, 14)));
    }

    [Theory]
    [InlineData(24, 14, 0, 25, 3)]   // 11:00 de 24/09 em Brasília → meia-noite de 25/09 = 03:00 UTC
    [InlineData(25, 2, 59, 25, 3)]   // 23:59 de 24/09 em Brasília, já 25/09 em UTC → ainda 25/09 03:00
    [InlineData(25, 3, 0, 26, 3)]    // exatamente na meia-noite → a próxima, não ela mesma
    [InlineData(25, 3, 1, 26, 3)]
    public void ProximaMeiaNoite_NoHorarioDeBrasilia(int dia, int hora, int minuto, int diaEsperado, int horaEsperada)
    {
        Assert.Equal(Utc(diaEsperado, horaEsperada), CalendarioColeta.ProximaMeiaNoiteUtc(Utc(dia, hora, minuto), Brasilia));
    }

    [Fact]
    public void MeiaNoiteInexistente_UsaOPrimeiroInstanteValidoDoDia()
    {
        // Fuso fictício que adianta o relógio à 00:00 do dia 01/11 (como o horário de verão
        // brasileiro até 2019): nesse dia a meia-noite não existe, o dia começa à 01:00.
        var regra = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2020, 1, 1), new DateTime(2030, 12, 31), TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 0, 0, 0), 11, 1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 0, 0, 0), 2, 15));
        var fuso = TimeZoneInfo.CreateCustomTimeZone("Teste-03-DST", TimeSpan.FromHours(-3), "Teste", "Teste", "Teste verão", [regra]);

        var virada = CalendarioColeta.ProximaMeiaNoiteUtc(new DateTime(2026, 10, 31, 20, 0, 0, DateTimeKind.Utc), fuso);

        // 01:00 local já no horário de verão (UTC-2) = 03:00 UTC.
        Assert.Equal(new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc), virada);
    }

    [Fact]
    public void InstanteForaDeUtc_Falha()
    {
        Assert.Throws<ArgumentException>(() => CalendarioColeta.ProximaMeiaNoiteUtc(DateTime.Now, Brasilia));
    }
}
