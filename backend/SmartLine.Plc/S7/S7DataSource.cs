using S7.Net;
using SmartLine.Core.Plc;
using SmartLine.Plc.Parsing;
using S7Plc = S7.Net.Plc;

namespace SmartLine.Plc.S7;

/// <summary>
/// Lê o DB Weihenstephan de uma máquina por protocolo S7 (PUT/GET).
///
/// Uma instância por máquina. Faz uma única leitura de bloco por ciclo, trazendo
/// os 88 bytes inteiros: assim status, alarme e contador vêm sempre do mesmo
/// instante, sem risco de creditar produção a um período de parada.
/// </summary>
/// <remarks>
/// Exige, do lado do PLC:
/// <list type="bullet">
/// <item>PUT/GET habilitado na CPU (afrouxa uma proteção — ver nota de segurança).</item>
/// <item>DB com "acesso otimizado" DESLIGADO, senão os offsets não existem.</item>
/// </list>
/// </remarks>
public sealed class S7DataSource : IPlcDataSource
{
    private readonly S7Options _opcoes;
    private readonly TimeProvider _tempo;

    private S7Plc? _plc;
    private bool _descartado;

    /// <param name="opcoes">Configuração de conexão. Validada já no construtor.</param>
    /// <param name="tempo">
    /// Fonte de tempo. Injetável para os testes conseguirem fixar o timestamp
    /// da leitura; em produção usa o relógio do sistema.
    /// </param>
    public S7DataSource(S7Options opcoes, TimeProvider? tempo = null)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        opcoes.Validar();

        _opcoes = opcoes;
        _tempo = tempo ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<LeituraPlc> LerAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_descartado, this);

        var plc = await GarantirConexaoAsync(cancellationToken).ConfigureAwait(false);

        byte[] buffer;
        try
        {
            buffer = await plc
                .ReadBytesAsync(
                    DataType.DataBlock,
                    _opcoes.NumeroDb,
                    startByteAdr: 0,
                    count: WsDataBlockLayout.TamanhoBytes,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Conexão provavelmente morta: derruba para a próxima chamada reconectar.
            Desconectar();

            throw new PlcIndisponivelException(
                _opcoes.Descricao,
                $"Falha ao ler o DB {_opcoes.NumeroDb}: {ex.Message}",
                ex);
        }

        if (buffer.Length < WsDataBlockLayout.TamanhoBytes)
        {
            Desconectar();

            throw new PlcIndisponivelException(
                _opcoes.Descricao,
                $"Leitura incompleta do DB {_opcoes.NumeroDb}: " +
                $"vieram {buffer.Length} bytes, esperados {WsDataBlockLayout.TamanhoBytes}. " +
                "Confira se o DB existe e se o acesso otimizado está desligado.");
        }

        return WsDataBlockParser.Parse(buffer, _tempo.GetUtcNow().UtcDateTime);
    }

    /// <summary>
    /// Devolve a conexão aberta, abrindo na primeira vez ou depois de uma queda.
    /// </summary>
    private async Task<S7Plc> GarantirConexaoAsync(CancellationToken cancellationToken)
    {
        if (_plc is { IsConnected: true })
            return _plc;

        Desconectar();

        var plc = new S7Plc(
            MapearCpu(_opcoes.Cpu),
            _opcoes.Ip,
            _opcoes.Porta,
            (short)_opcoes.Rack,
            (short)_opcoes.Slot)
        {
            ReadTimeout = _opcoes.TimeoutLeituraMs,
            WriteTimeout = _opcoes.TimeoutLeituraMs
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_opcoes.TimeoutConexaoMs);

        try
        {
            await plc.OpenAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            FecharSilenciosamente(plc);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // Estourou o CancelAfter, não o token de fora: é timeout de conexão.
            FecharSilenciosamente(plc);

            throw new PlcIndisponivelException(
                _opcoes.Descricao,
                $"Timeout de {_opcoes.TimeoutConexaoMs} ms ao conectar no PLC.",
                ex);
        }
        catch (Exception ex)
        {
            FecharSilenciosamente(plc);

            throw new PlcIndisponivelException(
                _opcoes.Descricao,
                $"Falha ao conectar no PLC: {ex.Message}. " +
                "Confira endereço, rack/slot e se PUT/GET está habilitado na CPU.",
                ex);
        }

        _plc = plc;
        return plc;
    }

    /// <summary>Fecha e descarta a conexão atual, se houver. Não propaga erro.</summary>
    private void Desconectar()
    {
        if (_plc is null)
            return;

        FecharSilenciosamente(_plc);
        _plc = null;
    }

    /// <summary>
    /// Fecha a conexão engolindo qualquer erro.
    /// O Plc do S7.Net não expõe Dispose() público — Close() é a forma
    /// suportada de liberar o socket.
    /// </summary>
    private static void FecharSilenciosamente(S7Plc plc)
    {
        try
        {
            plc.Close();
        }
        catch
        {
            // Fechando conexão já quebrada ou nunca aberta: não há o que tratar.
        }
    }

    private static CpuType MapearCpu(S7Options.ModeloCpu modelo) => modelo switch
    {
        S7Options.ModeloCpu.S71500 => CpuType.S71500,
        S7Options.ModeloCpu.S71200 => CpuType.S71200,
        S7Options.ModeloCpu.S7300 => CpuType.S7300,
        S7Options.ModeloCpu.S7400 => CpuType.S7400,
        S7Options.ModeloCpu.Logo0BA8 => CpuType.Logo0BA8,
        _ => throw new ArgumentOutOfRangeException(
            nameof(modelo), modelo, "Modelo de CPU não suportado.")
    };

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_descartado)
            return ValueTask.CompletedTask;

        _descartado = true;
        Desconectar();

        return ValueTask.CompletedTask;
    }
}