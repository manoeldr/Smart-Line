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
/// enfileirou e transforma cada uma em paradas, comunicação e produção; e
/// tem um relógio que cuida do que depende só do tempo passar.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ao subir</b>, faz a retomada (<see cref="IRetomadaColetaService"/>): as
/// coletas que estavam em andamento voltam com os últimos contadores, para a
/// primeira mensagem já creditar o que foi produzido com o backend fora.
/// </para>
/// <para>
/// <b>Caminho de uma mensagem</b>: parser do WISE → IP → máquina (a coleta em
/// andamento iniciada com esse IP) → fila daquela máquina. Na fila: coleta em andamento → máquina de
/// estados → eventos gravados pelo registrador. A produção apurada é somada em
/// memória e gravada na consolidação.
/// </para>
/// <para>
/// <b>Relógio</b> (a cada <see cref="OpcoesColetaIot.IntervaloVerificacao"/>):
/// para cada máquina com coleta ligada, verifica comunicação e reclassificação
/// por tempo, consolida a produção a cada
/// <see cref="OpcoesColetaIot.IntervaloConsolidacao"/> (alinhado ao relógio:
/// 11:00, 11:05...) e vira o dia à meia-noite. A virada também é conferida
/// antes de cada mensagem, para uma amostra de depois da meia-noite nunca cair
/// na sessão do dia anterior.
/// </para>
/// <para>
/// <b>Uma fila por máquina.</b> Mensagens, relógio, finalização e desligamento
/// entram na mesma fila e são tratados um por vez, na ordem: a máquina de
/// estados não é thread-safe e a ordem importa. Uma gravação lenta numa
/// máquina não atrasa as outras.
/// </para>
/// <para>
/// <b>Finalizar</b> deve passar por <see cref="FinalizarAsync"/>: grava a
/// produção pendente antes de encerrar. Uma coleta finalizada por fora é
/// percebida no próximo ciclo e o estado é descartado (com o pendente).
/// <b>Ao desligar</b> o backend, a produção pendente de todas as máquinas é gravada.
/// </para>
/// <para>
/// Nenhum erro derruba o serviço: o que não dá para processar vai para o log
/// e o próximo comando segue normalmente.
/// </para>
/// </remarks>
public sealed class ColetaIotService : BackgroundService
{
    /// <summary>Amostras esperando numa fila; acima disso, a gravação daquela máquina travou.</summary>
    private const int LimiteAmostrasNaFila = 1_000;

    private readonly CaixaDeEntradaMqtt _caixa;
    private readonly IServiceScopeFactory _escopos;
    private readonly TimeProvider _tempo;
    private readonly OpcoesColetaIot _opcoes;
    private readonly ILogger<ColetaIotService> _log;

    private readonly ConcurrentDictionary<Guid, FilaMaquina> _filas = new();
    private readonly object _criacaoDeFilas = new();
    private bool _encerrando; // protegido por _criacaoDeFilas

    // Só o despachante (um fluxo só) mexe nestes.
    private readonly HashSet<string> _ipsLivresAvisados = new();
    private readonly HashSet<string> _ipsComProblemaAvisados = new();
    private readonly Dictionary<string, DateTime> _ultimaMensagemAnotada = new();

    private readonly ConcurrentDictionary<Guid, SituacaoColetaIot> _situacoes = new();
    private readonly ConcurrentDictionary<string, WiseVisto> _vistos = new();
    private readonly LeiturasEntradasWise _leituras = new();
    private readonly TaskCompletionSource _retomadaConcluida = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _processadas;
    private long _descartadas;

    public ColetaIotService(
        CaixaDeEntradaMqtt caixa,
        IServiceScopeFactory escopos,
        TimeProvider tempo,
        OpcoesColetaIot opcoes,
        ILogger<ColetaIotService> log)
    {
        _caixa = caixa;
        _escopos = escopos;
        _tempo = tempo;
        _opcoes = opcoes;
        _log = log;
    }

    /// <summary>Mensagens que chegaram à máquina de estados desde que o backend subiu.</summary>
    public long MensagensProcessadas => Interlocked.Read(ref _processadas);

    /// <summary>
    /// Mensagens que não viraram amostra de nenhuma coleta: tópico que não é de
    /// dados, conteúdo inválido, WISE livre (sem medição com o IP dele),
    /// fila da máquina travada, erro ao processar.
    /// </summary>
    public long MensagensDescartadas => Interlocked.Read(ref _descartadas);

    /// <summary>Concluída quando a retomada terminou e as mensagens começam a ser consumidas.</summary>
    public Task RetomadaConcluida => _retomadaConcluida.Task;

    /// <summary>Estado atual das máquinas em coleta.</summary>
    public IReadOnlyCollection<SituacaoColetaIot> Situacoes() => _situacoes.Values.ToList();

    /// <summary>
    /// Todo IP que publicou no broker desde que o backend subiu, em medição ou
    /// livre, com a última mensagem de cada um. É o que a tela Dispositivos IoT
    /// mostra e o que o Configurar medição sugere.
    /// </summary>
    public IReadOnlyCollection<WiseVisto> WiseVistos() =>
        _vistos.Values.OrderBy(w => w.EnderecoIp).ToList();

    /// <summary>
    /// Últimas leituras das entradas de um WISE (em medição ou livre), para o
    /// Validar entradas. Nulo se nada chegou desse IP desde que o backend subiu.
    /// </summary>
    public LeituraEntradas? LeiturasDoWise(string enderecoIp) => _leituras.Obter(enderecoIp);

    /// <summary>Estado atual de uma máquina; nulo se ela não está em coleta.</summary>
    public SituacaoColetaIot? Situacao(Guid maquinaLinhaId) =>
        _situacoes.TryGetValue(maquinaLinhaId, out var situacao) ? situacao : null;

    /// <summary>
    /// Finaliza uma coleta gravando antes a produção pendente. É por aqui que a
    /// API finaliza. Mesmos resultados de <see cref="IAcompanhamentoService.FinalizarAsync"/>.
    /// </summary>
    /// <remarks>
    /// Entra na fila da máquina: espera a mensagem em processamento terminar.
    /// Cancelar a espera não desfaz a finalização, que segue na fila.
    /// </remarks>
    public async Task<ResultadoFinalizacao> FinalizarAsync(
        Guid acompanhamentoId,
        Guid usuarioId,
        bool podeFinalizarDeOutros,
        CancellationToken cancellationToken = default)
    {
        Guid? maquina;
        await using (var escopo = _escopos.CreateAsyncScope())
        {
            maquina = await escopo.ServiceProvider.GetRequiredService<ILocalizadorColetaIot>()
                .MaquinaDoAcompanhamentoAsync(acompanhamentoId, cancellationToken);
        }
        if (maquina is null)
            return ResultadoFinalizacao.NaoEncontrado;

        var resultado = new TaskCompletionSource<ResultadoFinalizacao>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fila = Fila(maquina.Value);
        if (fila is not null && fila.Enviar(new ComandoFinalizar(acompanhamentoId, usuarioId, podeFinalizarDeOutros, resultado)))
            return await resultado.Task.WaitAsync(cancellationToken);

        // Motor desligando: a produção pendente já foi (ou está sendo) gravada no desligamento.
        await using (var escopo = _escopos.CreateAsyncScope())
        {
            return await escopo.ServiceProvider.GetRequiredService<IAcompanhamentoService>()
                .FinalizarAsync(acompanhamentoId, usuarioId, podeFinalizarDeOutros, cancellationToken);
        }
    }

    /// <summary>
    /// Um ciclo do relógio, agora: verifica todas as máquinas com coleta ligada
    /// (inclusive as que ainda não mandaram mensagem). Devolve quando todas
    /// terminaram. O relógio automático chama a cada
    /// <see cref="OpcoesColetaIot.IntervaloVerificacao"/>; os testes chamam na mão.
    /// </summary>
    public async Task VerificarAgoraAsync(CancellationToken cancellationToken = default)
    {
        var agora = Agora();
        IReadOnlyList<ColetaEmAndamento> coletas;
        await using (var escopo = _escopos.CreateAsyncScope())
        {
            coletas = await escopo.ServiceProvider.GetRequiredService<ILocalizadorColetaIot>()
                .ColetasEmAndamentoAsync(cancellationToken);
        }

        var porMaquina = coletas.ToDictionary(c => c.MaquinaLinhaId);
        foreach (var coleta in coletas)
            Fila(coleta.MaquinaLinhaId); // coleta sem nenhuma mensagem ainda também é verificada

        var ciclos = new List<Task>();
        foreach (var fila in _filas.Values)
        {
            var concluido = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (fila.Enviar(new ComandoRelogio(agora, porMaquina.GetValueOrDefault(fila.MaquinaLinhaId), concluido)))
                ciclos.Add(concluido.Task);
        }

        await Task.WhenAll(ciclos).WaitAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken parar)
    {
        var relogio = Task.CompletedTask;
        try
        {
            await RetomarAsync(parar);
            _retomadaConcluida.TrySetResult();
            relogio = RodarRelogioAsync(parar);

            await foreach (var mensagem in _caixa.Leitor.ReadAllAsync(parar))
                await DespacharAsync(mensagem, parar);
        }
        catch (OperationCanceledException) when (parar.IsCancellationRequested)
        {
        }
        finally
        {
            _retomadaConcluida.TrySetResult();
            await relogio;
            await EncerrarFilasAsync();
        }
    }

    private DateTime Agora() => _tempo.GetUtcNow().UtcDateTime;

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
            _log.LogError(ex, "Falha ao retomar as coletas em andamento. Elas serão reconhecidas pelo relógio ou na próxima mensagem de cada WISE, sem o crédito da produção feita com o backend fora.");
            return;
        }

        var agora = Agora();
        foreach (var r in retomadas)
        {
            if (r.Erro is not null)
            {
                _log.LogError("Coleta {AcompanhamentoId} não pôde ser retomada: {Erro}", r.AcompanhamentoId, r.Erro);
                continue;
            }

            // A retomada já fez as viradas atrasadas: a próxima é depois de agora.
            var coleta = new ColetaEmCurso(r.AcompanhamentoId, r.CriarEstado(), agora, agora, _opcoes);
            Fila(r.MaquinaLinhaId)!.Atual = coleta;
            Publicar(r.MaquinaLinhaId, coleta);
        }

        if (retomadas.Count > 0)
            _log.LogInformation("Coletas retomadas: {Quantidade}.", retomadas.Count(r => r.Erro is null));
    }

    // ── Relógio ─────────────────────────────────────────────────────

    private async Task RodarRelogioAsync(CancellationToken parar)
    {
        if (_opcoes.IntervaloVerificacao == Timeout.InfiniteTimeSpan)
            return;

        try
        {
            using var relogio = new PeriodicTimer(_opcoes.IntervaloVerificacao, _tempo);
            while (await relogio.WaitForNextTickAsync(parar))
            {
                try
                {
                    await VerificarAgoraAsync(parar);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.LogError(ex, "Falha no relógio da coleta; tenta de novo no próximo ciclo.");
                }
            }
        }
        catch (OperationCanceledException) when (parar.IsCancellationRequested)
        {
        }
    }

    // ── Despacho: mensagem → máquina ────────────────────────────────

    private async Task DespacharAsync(MensagemMqtt mensagem, CancellationToken parar)
    {
        _vistos.AddOrUpdate(
            mensagem.EnderecoIp,
            ip => new WiseVisto(ip, mensagem.ClientId, mensagem.Topico, mensagem.RecebidaEmUtc, 1),
            (_, antes) => antes with
            {
                ClientId = mensagem.ClientId,
                Topico = mensagem.Topico,
                UltimaMensagemUtc = mensagem.RecebidaEmUtc,
                Mensagens = antes.Mensagens + 1
            });

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

        _leituras.Registrar(mensagem.EnderecoIp, resultado.Amostra!);

        Guid? maquina;
        try
        {
            await using var escopo = _escopos.CreateAsyncScope();
            var localizador = escopo.ServiceProvider.GetRequiredService<ILocalizadorColetaIot>();
            maquina = await localizador.MaquinaDoWiseAsync(mensagem.EnderecoIp, parar);
            if (maquina is not null)
                await AnotarUltimaMensagemAsync(localizador, mensagem, parar);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Interlocked.Increment(ref _descartadas);
            _log.LogError(ex, "Falha ao localizar a medição do WISE {EnderecoIp}; mensagem descartada.", mensagem.EnderecoIp);
            return;
        }

        if (maquina is null)
        {
            // WISE livre: ligado, mas nenhuma medição em andamento com o IP dele. Normal
            // (ex.: instalado e aguardando o início, ou recém-finalizado). Um registro por IP:
            // um WISE publica a cada poucos segundos.
            Interlocked.Increment(ref _descartadas);
            if (_ipsLivresAvisados.Add(mensagem.EnderecoIp))
            {
                _log.LogInformation(
                    "WISE {EnderecoIp} (ClientId {ClientId}) publicando sem medição em andamento com esse IP. " +
                    "As mensagens dele são descartadas até uma medição Semi Automática ser iniciada com esse IP.",
                    mensagem.EnderecoIp, mensagem.ClientId);
            }
            return;
        }

        if (_ipsLivresAvisados.Remove(mensagem.EnderecoIp))
            _log.LogInformation("WISE {EnderecoIp} agora está na medição da máquina {MaquinaLinhaId}.", mensagem.EnderecoIp, maquina.Value);

        var fila = Fila(maquina.Value);
        if (fila is null || !fila.EnviarAmostra(resultado.Amostra!))
        {
            Interlocked.Increment(ref _descartadas);
            if (fila is not null)
                _log.LogWarning("Fila da máquina {MaquinaLinhaId} cheia (gravação travada?); mensagem descartada.", maquina.Value);
        }
    }

    /// <summary>Anota na coleta, no máximo uma vez por intervalo por WISE. Falha aqui não impede a coleta.</summary>
    private async Task AnotarUltimaMensagemAsync(ILocalizadorColetaIot localizador, MensagemMqtt mensagem, CancellationToken parar)
    {
        if (_ultimaMensagemAnotada.TryGetValue(mensagem.EnderecoIp, out var anotada)
            && mensagem.RecebidaEmUtc - anotada < _opcoes.IntervaloUltimaMensagem)
            return;

        try
        {
            await localizador.RegistrarUltimaMensagemAsync(mensagem.EnderecoIp, mensagem.RecebidaEmUtc, parar);
            _ultimaMensagemAnotada[mensagem.EnderecoIp] = mensagem.RecebidaEmUtc;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Não foi possível anotar a última mensagem do WISE {EnderecoIp}.", mensagem.EnderecoIp);
        }
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

    // ── Filas ───────────────────────────────────────────────────────

    /// <summary>Fila da máquina, criada na primeira vez. Nulo depois que o motor começou a desligar.</summary>
    private FilaMaquina? Fila(Guid maquinaLinhaId)
    {
        if (_filas.TryGetValue(maquinaLinhaId, out var existente))
            return existente;

        lock (_criacaoDeFilas)
        {
            if (_encerrando)
                return null;

            return _filas.GetOrAdd(maquinaLinhaId, id =>
            {
                var fila = new FilaMaquina(id);
                fila.Tarefa = Task.Run(() => ConsumirAsync(fila));
                return fila;
            });
        }
    }

    private async Task EncerrarFilasAsync()
    {
        lock (_criacaoDeFilas)
            _encerrando = true;

        var agora = Agora();
        foreach (var fila in _filas.Values)
        {
            fila.Enviar(new ComandoDesligar(agora));
            fila.Fechar();
        }

        await Task.WhenAll(_filas.Values.Select(f => f.Tarefa));
    }

    private async Task ConsumirAsync(FilaMaquina fila)
    {
        await foreach (var comando in fila.Leitor.ReadAllAsync())
        {
            try
            {
                switch (comando)
                {
                    case ComandoAmostra c:
                        fila.AmostraRetirada();
                        await ProcessarAmostraAsync(fila, c.Amostra);
                        break;
                    case ComandoRelogio c:
                        await ProcessarRelogioAsync(fila, c.AgoraUtc, c.Coleta);
                        break;
                    case ComandoFinalizar c:
                        c.Resultado.TrySetResult(await FinalizarNaFilaAsync(fila, c));
                        break;
                    case ComandoDesligar c:
                        await DesligarNaFilaAsync(fila, c.AgoraUtc);
                        break;
                }
            }
            catch (Exception ex)
            {
                if (comando is ComandoAmostra)
                    Interlocked.Increment(ref _descartadas);
                if (comando is ComandoFinalizar f)
                    f.Resultado.TrySetException(ex);
                _log.LogError(ex, "Falha na coleta da máquina {MaquinaLinhaId} ({Comando}); segue com o próximo.",
                    fila.MaquinaLinhaId, comando.GetType().Name);
            }
            finally
            {
                if (comando is ComandoRelogio r)
                    r.Concluido.TrySetResult();
            }
        }
    }

    // ── Comandos, dentro da fila da máquina ─────────────────────────

    private async Task ProcessarAmostraAsync(FilaMaquina fila, AmostraWise amostra)
    {
        await using var escopo = _escopos.CreateAsyncScope();
        var servicos = escopo.ServiceProvider;

        var emAndamento = await servicos.GetRequiredService<ILocalizadorColetaIot>().ColetaEmAndamentoAsync(fila.MaquinaLinhaId);
        var coleta = await SincronizarAsync(servicos, fila, emAndamento, amostra.TimestampUtc);
        if (coleta is null)
        {
            // WISE ligado numa máquina sem coleta: normal, não é erro.
            Interlocked.Increment(ref _descartadas);
            return;
        }

        await VirarSeChegouAHoraAsync(servicos, coleta, amostra.TimestampUtc);

        var eventos = coleta.Estado.Processar(amostra);
        coleta.AmostraDesdeConsolidacao = true;

        // Produção primeiro: fica em memória mesmo se a gravação dos eventos falhar.
        foreach (var evento in eventos)
        {
            switch (evento)
            {
                case ProducaoApurada { SemComunicacao: false } p:
                    coleta.Pendente = coleta.Pendente.Somar(p);
                    break;
                // A feita sem comunicação o registrador grava já, numa leitura própria.
                case ContadorReiniciado c:
                    _log.LogWarning(
                        "Contador {Contador} da máquina {MaquinaLinhaId} voltou de {Anterior} para {Novo} (WISE reiniciado?). " +
                        "Os pulsos até o reinício que ainda não tinham sido lidos se perdem.",
                        c.Contador, fila.MaquinaLinhaId, c.ValorAnterior, c.ValorNovo);
                    break;
            }
        }

        Publicar(fila.MaquinaLinhaId, coleta);
        await RegistrarAsync(servicos, coleta, eventos);

        // Contada só depois de gravada: quem espera por este número já encontra tudo no banco.
        Interlocked.Increment(ref _processadas);
    }

    private async Task ProcessarRelogioAsync(FilaMaquina fila, DateTime agora, ColetaEmAndamento? emAndamento)
    {
        if (emAndamento is null && fila.Atual is null)
            return; // máquina sem coleta: nada a fazer

        await using var escopo = _escopos.CreateAsyncScope();
        var servicos = escopo.ServiceProvider;

        var coleta = await SincronizarAsync(servicos, fila, emAndamento, agora);
        if (coleta is null)
            return;

        await VirarSeChegouAHoraAsync(servicos, coleta, agora);

        // Relógio atrás da última amostra (só em teste, com instantes escolhidos): nada a verificar.
        if (coleta.Estado.UltimaAmostraUtc is not { } ultima || agora >= ultima)
            await RegistrarAsync(servicos, coleta, coleta.Estado.Verificar(agora));

        if (agora >= coleta.ProximaConsolidacaoUtc)
        {
            await ConsolidarAsync(servicos, coleta, agora);
            coleta.ProximaConsolidacaoUtc = ProximoMultiplo(agora, _opcoes.IntervaloConsolidacao);
        }

        Publicar(fila.MaquinaLinhaId, coleta);
    }

    private async Task<ResultadoFinalizacao> FinalizarNaFilaAsync(FilaMaquina fila, ComandoFinalizar comando)
    {
        await using var escopo = _escopos.CreateAsyncScope();
        var servicos = escopo.ServiceProvider;
        var agora = Agora();

        if (fila.Atual is { } coleta && coleta.AcompanhamentoId == comando.AcompanhamentoId)
        {
            // Gravar antes de conferir a permissão é inofensivo: é só produção que já aconteceu.
            await VirarSeChegouAHoraAsync(servicos, coleta, agora);
            await ConsolidarAsync(servicos, coleta, agora);
        }

        var resultado = await servicos.GetRequiredService<IAcompanhamentoService>()
            .FinalizarAsync(comando.AcompanhamentoId, comando.UsuarioId, comando.PodeFinalizarDeOutros);

        if (resultado == ResultadoFinalizacao.Finalizado && fila.Atual?.AcompanhamentoId == comando.AcompanhamentoId)
        {
            fila.Atual = null;
            _situacoes.TryRemove(fila.MaquinaLinhaId, out _);
        }

        return resultado;
    }

    private async Task DesligarNaFilaAsync(FilaMaquina fila, DateTime agora)
    {
        if (fila.Atual is not { } coleta)
            return;

        await using var escopo = _escopos.CreateAsyncScope();
        await VirarSeChegouAHoraAsync(escopo.ServiceProvider, coleta, agora);
        await ConsolidarAsync(escopo.ServiceProvider, coleta, agora);
    }

    // ── Peças comuns ────────────────────────────────────────────────

    /// <summary>
    /// Alinha o estado da fila com a coleta em andamento no banco: descobre a
    /// coleta iniciada, descarta a finalizada, troca se for outra.
    /// </summary>
    private async Task<ColetaEmCurso?> SincronizarAsync(
        IServiceProvider servicos, FilaMaquina fila, ColetaEmAndamento? emAndamento, DateTime agora)
    {
        if (emAndamento is null)
        {
            if (fila.Atual is { } anterior)
            {
                if (anterior.Pendente.Vazia)
                {
                    _log.LogInformation("Coleta {AcompanhamentoId} da máquina {MaquinaLinhaId} não está mais em andamento.",
                        anterior.AcompanhamentoId, fila.MaquinaLinhaId);
                }
                else
                {
                    _log.LogWarning(
                        "Coleta {AcompanhamentoId} da máquina {MaquinaLinhaId} foi finalizada sem passar pelo motor; " +
                        "{Garrafas} garrafas e {Rejeito} de rejeito ainda não gravadas se perderam.",
                        anterior.AcompanhamentoId, fila.MaquinaLinhaId, anterior.Pendente.Garrafas, anterior.Pendente.Rejeito);
                }
                fila.Atual = null;
                _situacoes.TryRemove(fila.MaquinaLinhaId, out _);
            }
            return null;
        }

        if (fila.Atual?.AcompanhamentoId == emAndamento.AcompanhamentoId)
            return fila.Atual;

        fila.Atual = await CarregarColetaAsync(servicos, fila, emAndamento, agora);
        if (fila.Atual is null)
            _situacoes.TryRemove(fila.MaquinaLinhaId, out _);
        else
            Publicar(fila.MaquinaLinhaId, fila.Atual);
        return fila.Atual;
    }

    private async Task<ColetaEmCurso?> CarregarColetaAsync(
        IServiceProvider servicos, FilaMaquina fila, ColetaEmAndamento emAndamento, DateTime agora)
    {
        try
        {
            var configuracao = await servicos.GetRequiredService<IAcompanhamentoService>()
                .CarregarConfiguracaoAsync(emAndamento.AcompanhamentoId);
            _log.LogInformation("Coleta {AcompanhamentoId} da máquina {MaquinaLinhaId} acompanhada pelo motor.",
                emAndamento.AcompanhamentoId, fila.MaquinaLinhaId);
            fila.ErroAvisado = null;

            // Virada contada a partir do início da coleta: se ela começou ontem e só agora
            // apareceu, a sessão de ontem ainda precisa virar (a virada é idempotente).
            var estado = new EstadoMaquinaIot(configuracao, inicioColetaUtc: emAndamento.IniciadoEmUtc);
            return new ColetaEmCurso(emAndamento.AcompanhamentoId, estado, agora, emAndamento.IniciadoEmUtc, _opcoes);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            if (fila.ErroAvisado != emAndamento.AcompanhamentoId)
            {
                fila.ErroAvisado = emAndamento.AcompanhamentoId;
                _log.LogError("Coleta {AcompanhamentoId} da máquina {MaquinaLinhaId} com configuração inválida; mensagens descartadas: {Erro}",
                    emAndamento.AcompanhamentoId, fila.MaquinaLinhaId, ex.Message);
            }
            return null;
        }
    }

    /// <summary>Faz as viradas de meia-noite que já passaram, levando o pendente para a sessão que fecha.</summary>
    private async Task VirarSeChegouAHoraAsync(IServiceProvider servicos, ColetaEmCurso coleta, DateTime instante)
    {
        while (instante >= coleta.ProximaViradaUtc)
        {
            var virada = coleta.ProximaViradaUtc;
            await servicos.GetRequiredService<IRegistradorColeta>()
                .VirarDiaAsync(coleta.AcompanhamentoId, virada, coleta.Pendente, ContadoresBrutos(coleta));
            coleta.Pendente = ProducaoPendente.Nenhuma;
            coleta.AmostraDesdeConsolidacao = false;
            coleta.ProximaViradaUtc = CalendarioColeta.ProximaMeiaNoiteUtc(virada, _opcoes.Fuso);
            _log.LogInformation("Coleta {AcompanhamentoId}: virada do dia em {Virada:u}.", coleta.AcompanhamentoId, virada);
        }
    }

    /// <summary>Grava o pendente como leitura. Sem produção nem amostra nova desde a última vez, não faz nada.</summary>
    private static async Task ConsolidarAsync(IServiceProvider servicos, ColetaEmCurso coleta, DateTime agora)
    {
        if (coleta.Pendente.Vazia && !coleta.AmostraDesdeConsolidacao)
            return; // WISE calado: não há o que gravar, nem contador novo

        await servicos.GetRequiredService<IRegistradorColeta>()
            .ConsolidarProducaoAsync(coleta.AcompanhamentoId, agora, coleta.Pendente, ContadoresBrutos(coleta));
        coleta.Pendente = ProducaoPendente.Nenhuma;
        coleta.AmostraDesdeConsolidacao = false;
    }

    private static async Task RegistrarAsync(IServiceProvider servicos, ColetaEmCurso coleta, IReadOnlyList<EventoColeta> eventos)
    {
        if (eventos.Count > 0)
            await servicos.GetRequiredService<IRegistradorColeta>().RegistrarAsync(coleta.AcompanhamentoId, eventos);
    }

    private static Dictionary<CanalWise, uint>? ContadoresBrutos(ColetaEmCurso coleta) =>
        coleta.Estado.ContadoresBrutos.Count == 0 ? null : new Dictionary<CanalWise, uint>(coleta.Estado.ContadoresBrutos);

    /// <summary>Próximo múltiplo do intervalo depois do instante (11:03 com 5 min → 11:05).</summary>
    private static DateTime ProximoMultiplo(DateTime instanteUtc, TimeSpan intervalo) =>
        new((instanteUtc.Ticks / intervalo.Ticks + 1) * intervalo.Ticks, DateTimeKind.Utc);

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

    // ── Tipos internos ──────────────────────────────────────────────

    private abstract record Comando;
    private sealed record ComandoAmostra(AmostraWise Amostra) : Comando;
    private sealed record ComandoRelogio(DateTime AgoraUtc, ColetaEmAndamento? Coleta, TaskCompletionSource Concluido) : Comando;
    private sealed record ComandoFinalizar(
        Guid AcompanhamentoId, Guid UsuarioId, bool PodeFinalizarDeOutros,
        TaskCompletionSource<ResultadoFinalizacao> Resultado) : Comando;
    private sealed record ComandoDesligar(DateTime AgoraUtc) : Comando;

    /// <summary>Coleta de um acompanhamento: estado da máquina, produção ainda não gravada e agenda.</summary>
    private sealed class ColetaEmCurso
    {
        public ColetaEmCurso(Guid acompanhamentoId, EstadoMaquinaIot estado, DateTime agoraUtc, DateTime viradaDesdeUtc, OpcoesColetaIot opcoes)
        {
            AcompanhamentoId = acompanhamentoId;
            Estado = estado;
            ProximaConsolidacaoUtc = ProximoMultiplo(agoraUtc, opcoes.IntervaloConsolidacao);
            ProximaViradaUtc = CalendarioColeta.ProximaMeiaNoiteUtc(viradaDesdeUtc, opcoes.Fuso);
        }

        public Guid AcompanhamentoId { get; }
        public EstadoMaquinaIot Estado { get; }
        public ProducaoPendente Pendente { get; set; }

        /// <summary>Chegou amostra depois da última consolidação (há contador bruto novo a guardar).</summary>
        public bool AmostraDesdeConsolidacao { get; set; }

        public DateTime ProximaConsolidacaoUtc { get; set; }
        public DateTime ProximaViradaUtc { get; set; }
    }

    /// <summary>Fila e estado de uma máquina. Só o consumidor da fila mexe em <see cref="Atual"/>, exceto na retomada (antes de qualquer comando).</summary>
    private sealed class FilaMaquina(Guid maquinaLinhaId)
    {
        private readonly Channel<Comando> _canal = Channel.CreateUnbounded<Comando>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        private int _amostrasNaFila;

        public Guid MaquinaLinhaId { get; } = maquinaLinhaId;
        public Task Tarefa { get; set; } = Task.CompletedTask;
        public ColetaEmCurso? Atual { get; set; }
        public Guid? ErroAvisado { get; set; }

        public ChannelReader<Comando> Leitor => _canal.Reader;

        /// <summary>Falso se a fila já foi fechada (motor desligando).</summary>
        public bool Enviar(Comando comando) => _canal.Writer.TryWrite(comando);

        /// <summary>
        /// Falso se a fila está fechada ou com amostras demais (gravação travada).
        /// Descartar a nova não perde produção: os contadores são acumulados.
        /// Relógio, finalização e desligamento nunca são descartados.
        /// </summary>
        public bool EnviarAmostra(AmostraWise amostra)
        {
            if (Interlocked.Increment(ref _amostrasNaFila) <= LimiteAmostrasNaFila
                && _canal.Writer.TryWrite(new ComandoAmostra(amostra)))
                return true;

            Interlocked.Decrement(ref _amostrasNaFila);
            return false;
        }

        public void AmostraRetirada() => Interlocked.Decrement(ref _amostrasNaFila);

        public void Fechar() => _canal.Writer.TryComplete();
    }
}
