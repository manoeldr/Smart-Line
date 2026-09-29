using System.Net;

namespace SmartLine.Core.Iot;

/// <summary>Ordena IPs numericamente: 192.168.10.9 antes de 192.168.10.21 (texto puro poria ao contrário).</summary>
public sealed class ComparadorIp : IComparer<string>
{
    public static readonly ComparadorIp Instancia = new();

    public int Compare(string? x, string? y)
    {
        var bx = Bytes(x);
        var by = Bytes(y);
        if (bx is null || by is null)
            return string.CompareOrdinal(x, y);
        if (bx.Length != by.Length)
            return bx.Length.CompareTo(by.Length);
        for (var i = 0; i < bx.Length; i++)
            if (bx[i] != by[i])
                return bx[i].CompareTo(by[i]);
        return 0;
    }

    private static byte[]? Bytes(string? ip) =>
        IPAddress.TryParse(ip, out var endereco) ? endereco.GetAddressBytes() : null;
}
