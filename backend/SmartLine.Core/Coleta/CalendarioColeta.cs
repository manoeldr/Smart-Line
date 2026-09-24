namespace SmartLine.Core.Coleta;

/// <summary>
/// Datas da coleta contínua. O banco guarda tudo em UTC, mas o "dia" da
/// sessão é o dia da fábrica: a virada acontece à meia-noite no fuso local.
/// </summary>
public static class CalendarioColeta
{
    /// <summary>
    /// Fuso das fábricas. IANA primeiro (Windows com ICU, que é o padrão do .NET
    /// desde a versão 5, e Linux); o id do Windows fica de reserva caso o ICU
    /// esteja desligado na máquina.
    /// </summary>
    public static TimeZoneInfo FusoPadrao { get; } = Buscar("America/Sao_Paulo", "E. South America Standard Time");

    /// <summary>
    /// Próxima meia-noite local estritamente depois de <paramref name="instanteUtc"/>, em UTC.
    /// </summary>
    /// <remarks>
    /// Se a meia-noite não existir no fuso (horário de verão começando às 00:00,
    /// como no Brasil até 2019), usa o primeiro instante válido do dia.
    /// </remarks>
    /// <exception cref="ArgumentException">Instante não está em UTC.</exception>
    public static DateTime ProximaMeiaNoiteUtc(DateTime instanteUtc, TimeZoneInfo fuso)
    {
        if (instanteUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Instante precisa estar em UTC.", nameof(instanteUtc));

        var local = TimeZoneInfo.ConvertTimeFromUtc(instanteUtc, fuso);
        var meiaNoite = DateTime.SpecifyKind(local.Date.AddDays(1), DateTimeKind.Unspecified);
        while (fuso.IsInvalidTime(meiaNoite))
            meiaNoite = meiaNoite.AddMinutes(30);

        return TimeZoneInfo.ConvertTimeToUtc(meiaNoite, fuso);
    }

    private static TimeZoneInfo Buscar(params string[] ids)
    {
        foreach (var id in ids)
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var fuso))
                return fuso;
        }

        // Sem base de fusos no sistema: Brasília sem horário de verão (vigente desde 2019).
        return TimeZoneInfo.CreateCustomTimeZone("Brasilia-03", TimeSpan.FromHours(-3), "Brasília (UTC-03)", "Brasília (UTC-03)");
    }
}
