using System.Security.Cryptography;
using System.Text;

namespace SmartLine.Core.Iot;

/// <summary>Uma regra padrão: sensor em alarme → motivo externo.</summary>
/// <param name="Chave">Identificação estável da regra, usada para derivar Ids. Nunca mudar.</param>
public sealed record RegraPadraoIot(string Chave, int Prioridade, CanalWise Sensor, string NomeMotivo);

/// <summary>
/// Regras padrão do Semi Automático, criadas para toda máquina do catálogo.
/// Todas são paradas Externas: o sensor mostra que a causa está fora da máquina.
/// </summary>
/// <remarks>
/// Os Ids do que o seed cria (motivos, conjunto, regras, condições) são
/// <b>determinísticos</b>, derivados do Id da máquina. Motivos e máquinas são
/// distribuídos entre o PC central e as estações pela exportação/importação,
/// que faz upsert por Id: com Ids aleatórios, cada estação criaria os seus e a
/// importação duplicaria os motivos padrão. Assim, todo computador gera o
/// mesmo Id para o mesmo item e a importação só encaixa.
/// </remarks>
public static class RegrasPadraoIot
{
    public static IReadOnlyList<RegraPadraoIot> Todas { get; } =
    [
        new("falta-garrafas-entrada", 1, CanalWise.S8, "Falta de garrafas na entrada"),
        new("abaixo-acumulo-minimo",  2, CanalWise.S1, "Abaixo do acúmulo mínimo"),
        new("acumulo-saida-garrafas", 3, CanalWise.S7, "Acúmulo na saída de garrafas"),
        new("acumulo-saida",          4, CanalWise.S4, "Acúmulo na saída (caixas/pallets)"),
    ];

    /// <summary>
    /// Id estável derivado de uma base e de uma chave (mesma entrada, mesmo Id,
    /// em qualquer computador). SHA-256 truncado, com os bits de versão e
    /// variante de um UUID v5, para não colidir com Ids gerados por
    /// <see cref="Guid.NewGuid"/> (v4).
    /// </summary>
    public static Guid IdDeterministico(Guid baseId, string chave)
    {
        var entrada = Encoding.UTF8.GetBytes($"smartline-regras-padrao|{baseId:N}|{chave}");
        var hash = SHA256.HashData(entrada);
        var bytes = hash.AsSpan(0, 16).ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50); // versão 5
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // variante RFC 4122
        return new Guid(bytes, bigEndian: true);
    }
}
