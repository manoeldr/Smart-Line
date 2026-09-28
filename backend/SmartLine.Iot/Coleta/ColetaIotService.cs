using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartLine.Core.Coleta;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using SmartLine.Iot.Broker;
using SmartLine.Iot.Wise;

namespace SmartLine.Iot.Coleta;

/// <summary>
/// Motor da coleta do Semi Automático: consome as mensagens que o broker
/// enfileirou e transforma cada uma em paradas, comunicação e produção.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ao subir</b>, faz a retomada (<see cref="IRetomadaColetaService"/>): as
/// coletas que estavam em andamento voltam com os últimos contadores, para a
/// primeira mensagem já creditar o que foi produzido com o backend fora.
/// </para>
/// <para>
/// <b>Caminho de uma mensagem</b>: parser do WISE → IP → máquina (cadastro do
/// WISE) → fila daquela máquina. Na fila: acompanhamento em andamento →
/// máquina de estados → eventos gravados pelo registrador; a produção apurada
/// é somada em memória (a consolidação periódica grava, passo 4.4b).
/// </para>
/// <para>
/// <b>Uma fila por máquina.</b> A máquina de estados não é thread-safe e a
/// ordem das amostras importa; cada máquina processa uma mensagem por vez, na
/// ordem de chegada, e uma gravação lenta numa máquina não atrasa as outras.
/// </para>
/// <para>
/// <b>Coleta iniciada depois</b> é descoberta na primeira mensagem (a fila vê
/// um acompanhamento que não conhecia e carrega a configuração). A primeira
/// amostra só vira referência dos contadores. <b>Coleta finalizada</b>: a fila
/// vê que não há mais acompanhamento e descarta o estado.
/// </para>
/// <para>
/// Nenhum erro derruba o serviço: mensagem que não dá para processar é
/// registrada no log e descartada, e a próxima segue normalmente.
/// </para>
/// </remarks>
public sealed class ColetaIotService : BackgroundService
{
    private readonly CaixaDeEntradaMqtt _caixa;
    private readonly IServiceScopeFactory _escopos;
    private readonly ILogger<ColetaIotService> _log;

    // Só o despachante (um fluxo só) mexe nestes três.
    private readonly Dictionary<Guid, FilaMaquina> _filas = new();
    private readonly HashSet<string> _ipsDesconhecidosAvisados = new();
    private readonly HashSet<string> _ipsComProblemaAvisados = new();

    private readonly ConcurrentDictionary<Guid, SituacaoColetaIot> _situacoes = new();
    private readonly TaskCompletionSource _retomadaConcluida = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _processadas;
    private long _descartadas;

    public ColetaIotService(CaixaDeEntradaMqtt caixa, IServiceScopeFactory escopos, ILogger<ColetaIotService> log)
    {
        _caixa = caixa;
        _escopos = escopos;
        _log = log;
    }

    /// <summary>Mensagens que chegaram à máquina de estados desde que o backend subiu.</summary>
    public long MensagensProcessadas => Interlocked.Read(ref _processadas);

    /// <summary>
    /// Mensagens que não viraram amostra de nenhuma coleta: tópico que não é de
    /// dados, conteúdo inválido, IP sem WISE cadastrado, máquina sem coleta ligada.
    /// </summary>
    public long MensagensDescartadas => Interlocked.Read(ref _descartadas);

    /// <summary>Concluída quando a retomada terminou e as mensagens começam a ser consumidas.</summary>
    public Task RetomadaConcluida => _retomadaConcluida.Task;

    /// <summary>Estado atual das máquinas em coleta.</summary>
    public IReadOnlyCollection<SituacaoColetaIot> Situacoes() => _situacoes.Values.ToList();

    /// <summary>Estado atual de uma máquina; nulo se ela não está em coleta (ou ainda não mandou nada).</summary>
    public SituacaoColetaIot? Situacao(Guid maquinaLinhaId) =>
        _situacoes.TryGetValue(maquinaLinhaId, out var situacao) ? situacao : null;

    protected override async Task ExecuteAsync(CancellationToken parar)
    {
        try
        {
            await RetomarAsync(parar);
            _retomadaConcluida.TrySetResult();

            await foreach (var mensagem in _caixa.Leitor.ReadAllAsync(parar))
                await DespacharAsync(mensagem, parar);
        }
        catch (OperationCanceledException) when (parar.IsCancellationRequested)
        {
        }
        finally
        {
            _retomadaConcluida.TrySetResult();
            foreach (var fila in _filas.Values)
                fila.Fechar();
            await Task.WhenAll(_filas.Values.Select(f => f.Tarefa));
        }
    }

    // ── Retomada ────────────────────────────────────────────────────

    private async Task RetomarAsync(CancellationToken parar)
    {
        IReadOnlyList<ColetaRetomada> retomadas;
        try
        {
            await using var escopo = _escopos.CreateAsyncScope();
            retomadas = await escopo.ServiceProvider.GetRequiredService<IRetomadaColetaService>().RetomarAsync(parar);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "Falha ao retomar as coletas em andamento. Elas serão reconhecidas na próxima mensagem de cada WISE, sem o crédito da produção feita com o backend fora.");
            return;
        }

        foreach (var r in retomadas)
        {
            if (r.Erro is not null)
            {
                _log.LogError("Coleta {AcompanhamentoId} não pôde ser retomada: {Erro}", r.AcompanhamentoId, r.Erro);
                continue;
            }

            var coleta = new ColetaEmCurso(r.AcompanhamentoId, r.CriarEstado());
            Fila(r.MaquinaLinhaId).Atual = coleta;
            Publicar(r.MaquinaLinhaId, coleta);
        }

        if (retomadas.Count > 0)
            _log.LogInformation("Coletas retomadas: {Quantidade}.", retomadas.Count(r => r.Erro is null));
    }

    // ── Despacho: mensagem → máquina ────────────────────────────────

    private async Task DespacharAsync(MensagemMqtt mensagem, CancellationToken parar)
    {
        var resultado = ParserWise.Interpretar(mensagem);
        switch (resultado.Tipo)
        {
            case TipoResultadoWise.Ignorada:
                Interlocked.Increment(ref _descartadas);
                return;

            case TipoResultadoWise.Invalida:
                Interlocked.Increment(ref _descartadas);
                AvisarProblema(mensagem, resultado.Motivo!);
                return;
        }

        if (resultado.Avisos.Count > 0)
            AvisarProblema(mensagem, string.Join(" ", resultado.Avisos));

        Guid? maquina;
        try
        {
            await using var escopo = _escopos.CreateAsyncScope();
            maquina = await escopo.ServiceProvider.GetRequiredService<ILocalizadorColetaIot>()
                .MaquinaDoDispositivoAsync(mensagem.EnderecoIp, parar);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Interlocked.Increment(ref _descartadas);
            _log.LogError(ex, "Falha ao localizar o WISE do IP {EnderecoIp}; mensagem descartada.", mensagem.EnderecoIp);
            return;
        }

        if (maquina is null)
        {
            Interlocked.Increment(ref _descartadas);
            // Um aviso por IP: um WISE sem cadastro publica a cada poucos segundos.
            if (_ipsDesconhecidosAvisados.Add(mensagem.EnderecoIp))
            {
                _log.LogWarning(
                    "Mensagem de um IP sem WISE ativo cadastrado: {EnderecoIp} (ClientId {ClientId}). " +
                    "Cadastre o WISE com esse IP na máquina em que ele está. As próximas mensagens deste IP serão descartadas sem novo aviso.",
                    mensagem.EnderecoIp, mensagem.ClientId);
            }
            return;
        }

        if (_ipsDesconhecidosAvisados.Remove(mensagem.EnderecoIp))
            _log.LogInformation("WISE do IP {EnderecoIp} agora está cadastrado.", mensagem.EnderecoIp);

        Fila(maquina.Value).Enviar(resultado.Amostra!);
    }

    private void AvisarProblema(MensagemMqtt mensagem, string motivo)
    {
        // Firmware ou configuração diferente do esperado se repete em toda mensagem:
        // avisa uma vez por IP, o resto vai para o nível de depuração.
        if (_ipsComProblemaAvisados.Add(mensagem.EnderecoIp))
        {
            _log.LogWarning(
                "Mensagem do WISE {EnderecoIp} fora do formato esperado (tópico {Topico}): {Motivo} " +
                "Conteúdo: {Payload}. Próximas ocorrências deste IP só no log de depuração.",
                mensagem.EnderecoIp, mensagem.Topico, motivo, mensagem.PayloadComoTexto);
        }
        else
        {
            _log.LogDebug("Mensagem do WISE {EnderecoIp} fora do formato: {Motivo}", mensagem.EnderecoIp, motivo);
        }
    }

    private FilaMaquina Fila(Guid maquinaLinhaId)
    {
        if (!_filas.TryGetValue(maquinaLinhaId, out var fila))
        {
            fila = new FilaMaquina(maquinaLinhaId);
            fila.Tarefa = Task.Run(() => ConsumirAsync(fila));
            _filas[maquinaLinhaId] = fila;
        }
        return fila;
    }

    // ── Fila de uma máquina ─────────────────────────────────────────

    private async Task ConsumirAsync(FilaMaquina fila)
    {
        await foreach (var amostra in fila.Leitor.ReadAllAsync())
        {
            try
            {
                await ProcessarAsync(fila, amostra);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Falha ao processar mensagem da máquina {MaquinaLinhaId}; segue com a próxima.", fila.MaquinaLinhaId);
            }
        }
    }

    private async Task ProcessarAsync(FilaMaquina fila, AmostraWise amostra)
    {
        await using var escopo = _escopos.CreateAsyncScope();
        var servicos = escopo.ServiceProvider;

        var acompanhamentoId = await servicos.GetRequiredService<ILocalizadorColetaIot>()
            .AcompanhamentoEmAndamentoAsync(fila.MaquinaLinhaId);

        if (acompanhamentoId is null)
        {
            // WISE ligado numa máquina sem coleta: normal, não é erro.
            if (fila.Atual is { } anterior)
            {
                _log.LogInformation("Coleta {AcompanhamentoId} da máquina {MaquinaLinhaId} não está mais em andamento.",
                    anterior.AcompanhamentoId, fila.MaquinaLinhaId);
                fila.Atual = null;
                _situacoes.TryRemove(fila.MaquinaLinhaId, out _);
            }
            Interlocked.Increment(ref _descartadas);
            return;
        }

        if (fila.Atual?.AcompanhamentoId != acompanhamentoId)
        {
            fila.Atual = await IniciarColetaAsync(servicos, fila, acompanhamentoId.Value);
            if (fila.Atual is null)
            {
                _situacoes.TryRemove(fila.MaquinaLinhaId, out _);
                Interlocked.Increment(ref _descartadas);
                return;
            }
        }

        var coleta = fila.Atual!;
        var eventos = coleta.Estado.Processar(amostra);

        // Produção primeiro: fica em memória mesmo se a gravação dos eventos falhar.
        foreach (var evento in eventos)
        {
            switch (evento)
            {
                case ProducaoApurada p:
                    coleta.Pendente = coleta.Pendente.Somar(p);
                    break;
                case ContadorReiniciado c:
                    _log.LogWarning(
                        "Contador {Contador} da máquina {MaquinaLinhaId} voltou de {Anterior} para {Novo} (WISE reiniciado?). " +
                        "Os pulsos até o reinício que ainda não tinham sido lidos se perdem.",
                        c.Contador, fila.MaquinaLinhaId, c.ValorAnterior, c.ValorNovo);
                    break;
            }
        }

        Publicar(fila.MaquinaLinhaId, coleta);

        if (eventos.Count > 0)
            await servicos.GetRequiredService<IRegistradorColeta>().RegistrarAsync(coleta.AcompanhamentoId, eventos);

        // Contada só depois de gravada: quem espera por este número já encontra tudo no banco.
        Interlocked.Increment(ref _processadas);
    }

    private async Task<ColetaEmCurso?> IniciarColetaAsync(IServiceProvider servicos, FilaMaquina fila, Guid acompanhamentoId)
    {
        try
        {
            var configuracao = await servicos.GetRequiredService<IAcompanhamentoService>()
                .CarregarConfiguracaoAsync(acompanhamentoId);
            _log.LogInformation("Coleta {AcompanhamentoId} da máquina {MaquinaLinhaId} recebendo dados do WISE.",
                acompanhamentoId, fila.MaquinaLinhaId);
            fila.ErroAvisado = null;
            return new ColetaEmCurso(acompanhamentoId, new EstadoMaquinaIot(configuracao));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            if (fila.ErroAvisado != acompanhamentoId)
            {
                fila.ErroAvisado = acompanhamentoId;
                _log.LogError("Coleta {AcompanhamentoId} da máquina {MaquinaLinhaId} com configuração inválida; mensagens descartadas: {Erro}",
                    acompanhamentoId, fila.MaquinaLinhaId, ex.Message);
            }
            return null;
        }
    }

    private void Publicar(Guid maquinaLinhaId, ColetaEmCurso coleta)
    {
        var e = coleta.Estado;
        _situacoes[maquinaLinhaId] = new SituacaoColetaIot(
            maquinaLinhaId,
            coleta.AcompanhamentoId,
            e.Situacao,
            e.ClassificacaoAtual,
            e.InicioParadaUtc,
            e.UltimaAmostraUtc,
            coleta.Pendente,
            new Dictionary<CanalWise, bool>(e.EstadosSensores),
            new Dictionary<CanalWise, uint>(e.ContadoresBrutos));
    }

    /// <summary>Coleta de um acompanhamento: estado da máquina e produção ainda não gravada.</summary>
    private sealed class ColetaEmCurso(Guid acompanhamentoId, EstadoMaquinaIot estado)
    {
        public Guid AcompanhamentoId { get; } = acompanhamentoId;
        public EstadoMaquinaIot Estado { get; } = estado;
        public ProducaoPendente Pendente { get; set; }
    }

    /// <summary>Fila e estado de uma máquina. Só o consumidor da fila mexe em <see cref="Atual"/> depois de criada.</summary>
    private sealed class FilaMaquina(Guid maquinaLinhaId)
    {
        // Limitada como a caixa de entrada: se a gravação desta máquina travar, perde as
        // amostras mais velhas em vez de crescer sem fim. Os contadores são acumulados,
        // então a produção não se perde; só a resolução do que aconteceu no intervalo.
        private readonly Channel<AmostraWise> _canal = Channel.CreateBounded<AmostraWise>(
            new BoundedChannelOptions(1_000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true
            });

        public Guid MaquinaLinhaId { get; } = maquinaLinhaId;
        public Task Tarefa { get; set; } = Task.CompletedTask;
        public ColetaEmCurso? Atual { get; set; }
        public Guid? ErroAvisado { get; set; }

        public ChannelReader<AmostraWise> Leitor => _canal.Reader;
        public void Enviar(AmostraWise amostra) => _canal.Writer.TryWrite(amostra);
        public void Fechar() => _canal.Writer.TryComplete();
    }
}
