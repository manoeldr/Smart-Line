using System.Buffers.Binary;
using SmartLine.Core.Plc;
using O = SmartLine.Plc.Parsing.WsDataBlockLayout.Offsets;

namespace SmartLine.Plc.Parsing;

/// <summary>
/// Converte o buffer bruto do DB Weihenstephan em <see cref="LeituraPlc"/>.
///
/// Função pura: sem I/O, sem estado, sem relógio. Toda a suíte de testes roda
/// sem PLC e sem rede.
///
/// PLC Siemens é big-endian (Motorola). Ler little-endian não gera exceção —
/// só devolve números absurdos (um UDInt de valor 1 vira 16.777.216), então o
/// erro passaria despercebido. Daí o uso explícito de BinaryPrimitives.
/// </summary>
public static class WsDataBlockParser
{
    /// <summary>
    /// Interpreta o buffer lido do PLC.
    /// </summary>
    /// <param name="buffer">
    /// Bytes do DB. Precisa ter pelo menos <see cref="WsDataBlockLayout.TamanhoBytes"/>;
    /// bytes extras no fim são ignorados.
    /// </param>
    /// <param name="timestampUtc">Instante da leitura, em UTC.</param>
    /// <exception cref="ArgumentException">Buffer menor que o DB.</exception>
    /// <exception cref="ArgumentException">Timestamp não está em UTC.</exception>
    public static LeituraPlc Parse(ReadOnlySpan<byte> buffer, DateTime timestampUtc)
    {
        if (buffer.Length < WsDataBlockLayout.TamanhoBytes)
        {
            throw new ArgumentException(
                $"Buffer tem {buffer.Length} bytes; o DB Weihenstephan precisa de " +
                $"{WsDataBlockLayout.TamanhoBytes}. Leitura S7 incompleta.",
                nameof(buffer));
        }

        if (timestampUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Timestamp precisa estar em UTC. Hora local aqui desalinha o gráfico " +
                "por hora e a virada de sessão da meia-noite.",
                nameof(timestampUtc));
        }

        return new LeituraPlc
        {
            TimestampUtc = timestampUtc,

            // Status
            CodigoModo = LerUDInt(buffer, O.ModoAtual),
            CodigoPrograma = LerUDInt(buffer, O.ProgramaAtual),
            CodigoEstado = LerUDInt(buffer, O.EstadoAtual),
            VelocidadeAtual = LerReal(buffer, O.VelocidadeAtual),
            VelocidadeSetada = LerReal(buffer, O.VelocidadeSetada),
            VelocidadeProjetada = LerReal(buffer, O.VelocidadeProjetada),
            RazaoProducao = LerUDInt(buffer, O.RazaoProducao),
            CodigoFalha = LerUDInt(buffer, O.CodigoFalha),

            // Produtos
            TipoPalete = LerUDInt(buffer, O.TipoPalete),
            TipoCaixa = LerUDInt(buffer, O.TipoCaixa),
            TipoGarrafa = LerUDInt(buffer, O.TipoGarrafa),
            TipoBebida = LerUDInt(buffer, O.TipoBebida),
            TipoPacote = LerUDInt(buffer, O.TipoPacote),

            // Contadores
            TotalPaletes = LerUDInt(buffer, O.TotalPaletes),
            TotalCaixas = LerUDInt(buffer, O.TotalCaixas),
            TotalGarrafas = LerUDInt(buffer, O.TotalGarrafas),
            TotalPacotes = LerUDInt(buffer, O.TotalPacotes),

            // Sanmartin
            NumeroReceita = LerUDInt(buffer, O.NumeroReceita),
            TotalHectolitros = LerReal(buffer, O.TotalHectolitros),
            ConsumoEnergia = LerReal(buffer, O.ConsumoEnergia),
            HorasOperacao = LerReal(buffer, O.HorasOperacao),
            TipoProduto = LerUDInt(buffer, O.TipoProduto)
        };
    }

    /// <summary>UDInt do S7 = uint de 32 bits, big-endian. Nunca int: acima de 2,1 bi vira negativo.</summary>
    private static uint LerUDInt(ReadOnlySpan<byte> buffer, int offset) =>
        BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(offset, 4));

    /// <summary>Real do S7 = IEEE-754 de 32 bits, big-endian.</summary>
    private static float LerReal(ReadOnlySpan<byte> buffer, int offset) =>
        BinaryPrimitives.ReadSingleBigEndian(buffer.Slice(offset, 4));
}