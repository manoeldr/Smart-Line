using System.Net;

namespace SmartLine.Iot.Simulacao;

/// <summary>O que o simulador faz ao ligar (os mesmos parâmetros da linha de comando do console).</summary>
/// <param name="Maquinas">Quantos WISE simular (1 a 50).</param>
/// <param name="IntervaloSegundos">Segundos entre publicações periódicas.</param>
/// <param name="GarrafasPorMinuto">Garrafas por minuto da linha rodando (um valor ou um por máquina).</param>
/// <param name="GarrafasPorPulso">Garrafas por pulso do contador de cada máquina (a que conta caixas manda menos pulsos).</param>
/// <param name="RejeitoPercentual">Refugo (S3) em % da produção.</param>
/// <param name="Broker">IP e porta do broker do SmartLine.</param>
/// <param name="PrimeiroIp">Máquina 1 sai por 127.0.0.{PrimeiroIp}, a 2 pelo seguinte…</param>
/// <param name="Automatico">Paradas e quedas de comunicação automáticas desde o início.</param>
/// <param name="PortaHttp">Interface HTTP de cada WISE simulado (zerar contadores); 0 desliga.</param>
public sealed record OpcoesSimulador(
    int Maquinas = 2,
    int IntervaloSegundos = 20,
    IReadOnlyList<double>? GarrafasPorMinuto = null,
    IReadOnlyList<double>? GarrafasPorPulso = null,
    double RejeitoPercentual = 1,
    string Broker = "127.0.0.1",
    int PortaBroker = 1883,
    int PrimeiroIp = 21,
    bool Automatico = true,
    OpcoesParadasAleatorias? Paradas = null,
    int PortaHttp = 80)
{
    public static double Da(IReadOnlyList<double>? lista, int maquina, double padrao) =>
        lista is null || lista.Count == 0 ? padrao : lista[Math.Min(maquina - 1, lista.Count - 1)];

    public double GarrafasPorMinutoDa(int maquina) => Da(GarrafasPorMinuto, maquina, 600);
    public double GarrafasPorPulsoDa(int maquina) => Da(GarrafasPorPulso, maquina, 1);

    /// <summary>O contador conta pulsos: uma máquina que conta caixas de 84 manda 84 vezes menos pulsos.</summary>
    public double PulsosPorMinutoDa(int maquina) => GarrafasPorMinutoDa(maquina) / Math.Max(GarrafasPorPulsoDa(maquina), 0.001);
}

/// <summary>Situação de uma máquina simulada, para a tela e o "status" do console.</summary>
public sealed record SituacaoMaquinaSimulada(
    int Numero,
    string EnderecoIp,
    CenarioSimulado Cenario,
    FalhaComunicacao? Falha,
    uint ContadorProducao,
    uint ContadorRejeito,
    bool Conectada,
    bool Automatico,
    DateTime? ProximaMudancaUtc,
    DateTime? ProximaMudancaComunicacaoUtc,
    double GarrafasPorMinuto,
    double GarrafasPorPulso,
    bool InterfaceHttp);

/// <summary>Ação sobre uma máquina simulada (as mesmas do console).</summary>
public enum AcaoSimulada
{
    Rodando, Falta, Minimo, Saida, Caixas, Parada, Desligar, SemRede, Reiniciar, Auto, Manual
}

/// <summary>
/// O simulador de WISE-4051 inteiro, para rodar dentro do backend (Área do Desenvolvedor) ou
/// no console: publica os contadores de cada máquina no broker, sorteia paradas e quedas de
/// comunicação, atende a interface HTTP (zerar contadores) e recebe comandos. Um só por vez;
/// <see cref="LigarAsync"/> com ele ligado desliga antes.
/// </summary>
public sealed class SimuladorWise : IAsyncDisposable
{
    // Comandos, sorteios e publicação mexem nas mesmas máquinas: um de cada vez.
    private readonly SemaphoreSlim _trava = new(1, 1);
    private readonly object _ligar = new();

    private Execucao? _execucao;

    /// <summary>Linhas de acompanhamento (o que o console imprime), também para o log do backend.</summary>
    public event Action<string>? Mensagem;

    /// <summary>Cada publicação periódica (uma linha por máquina a cada intervalo): o console mostra; o backend só em Debug.</summary>
    public event Action<string>? Publicacao;

    public bool Ligado => _execucao is not null;
    public OpcoesSimulador? Opcoes => _execucao?.Opcoes;
    public DateTime? LigadoEmUtc => _execucao?.LigadoEmUtc;

    public async Task LigarAsync(OpcoesSimulador opcoes, CancellationToken cancellationToken = default)
    {
        if (opcoes.Maquinas is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(opcoes), "Simule de 1 a 50 máquinas.");
        if (opcoes.IntervaloSegundos is < 1 or > 3600)
            throw new ArgumentOutOfRangeException(nameof(opcoes), "O intervalo de publicação vai de 1 s a 1 h.");

        await DesligarAsync();

        Execucao execucao;
        lock (_ligar)
        {
            if (_execucao is not null)
                throw new InvalidOperationException("O simulador já está ligado.");
            execucao = new Execucao(this, opcoes, _trava);
            _execucao = execucao;
        }

        execucao.Iniciar();
        Avisar($"Simulador ligado: {opcoes.Maquinas} WISE a partir de 127.0.0.{opcoes.PrimeiroIp}, publicação a cada {opcoes.IntervaloSegundos} s.");
        foreach (var p in execucao.Publicadores)
            await Publicar(execucao, p, "inicial", cancellationToken);
    }

    public async Task DesligarAsync()
    {
        Execucao? execucao;
        lock (_ligar)
        {
            execucao = _execucao;
            _execucao = null;
        }
        if (execucao is null) return;

        await execucao.PararAsync();
        Avisar("Simulador desligado.");
    }

    public IReadOnlyList<SituacaoMaquinaSimulada> Situacao()
    {
        var execucao = _execucao;
        if (execucao is null) return [];
        return execucao.Publicadores.Select(p => new SituacaoMaquinaSimulada(
            p.Maquina.Numero,
            p.EnderecoOrigem,
            p.Maquina.Cenario,
            p.Maquina.Falha,
            p.Maquina.ContadorProducao,
            p.Maquina.ContadorRejeito,
            p.Conectado,
            execucao.Automatico.Ligado(p.Maquina),
            execucao.Automatico.ProximaMudanca(p.Maquina),
            execucao.Automatico.ProximaMudancaComunicacao(p.Maquina),
            execucao.Opcoes.GarrafasPorMinutoDa(p.Maquina.Numero),
            execucao.Opcoes.GarrafasPorPulsoDa(p.Maquina.Numero),
            execucao.ServidoresHttp.Any(s => s.Maquina == p.Maquina))).ToList();
    }

    /// <summary>Aplica uma ação a uma máquina (<paramref name="numero"/> 1..N) ou a todas (0).</summary>
    public async Task ComandoAsync(int numero, AcaoSimulada acao, CancellationToken cancellationToken = default)
    {
        var execucao = _execucao ?? throw new InvalidOperationException("O simulador está desligado.");
        List<PublicadorWiseSimulado> alvos = numero == 0
            ? execucao.Publicadores
            : [execucao.Publicadores.FirstOrDefault(p => p.Maquina.Numero == numero)
                ?? throw new ArgumentOutOfRangeException(nameof(numero), $"Máquina {numero} não existe (1 a {execucao.Publicadores.Count}).")];

        foreach (var p in alvos)
        {
            var agora = DateTime.UtcNow;
            bool saiuDoAuto = false, publica = true;
            await _trava.WaitAsync(cancellationToken);
            try
            {
                switch (acao)
                {
                    case AcaoSimulada.Auto:
                        execucao.Automatico.Ligar(p.Maquina, agora);
                        publica = false;
                        break;
                    case AcaoSimulada.Manual:
                        execucao.Automatico.Desligar(p.Maquina, agora);
                        publica = false;
                        break;
                    case AcaoSimulada.Reiniciar:
                        p.Maquina.Reiniciar(agora);
                        break;
                    case AcaoSimulada.SemRede:
                        // A máquina segue no que estava fazendo; só a rede cai. Quem manda agora é o usuário.
                        saiuDoAuto = execucao.Automatico.Ligado(p.Maquina);
                        execucao.Automatico.Desligar(p.Maquina, agora);
                        p.Maquina.PerderComunicacao(FalhaComunicacao.SemRede, agora);
                        break;
                    default:
                        // Comando de cenário: quem manda agora é o usuário, até ele pedir "auto" de novo.
                        saiuDoAuto = execucao.Automatico.Ligado(p.Maquina);
                        execucao.Automatico.Desligar(p.Maquina, agora);
                        if (p.Maquina.Falha is not null)
                            p.Maquina.RecuperarComunicacao(agora);
                        p.Maquina.MudarCenario(Cenario(acao), agora);
                        break;
                }
            }
            finally { _trava.Release(); }

            Avisar(acao switch
            {
                AcaoSimulada.Auto => $"Máquina {p.Maquina.Numero}: paradas e quedas automáticas ligadas.",
                AcaoSimulada.Manual => $"Máquina {p.Maquina.Numero}: só por comando.",
                AcaoSimulada.Reiniciar => $"Máquina {p.Maquina.Numero}: WISE reiniciado, contadores em zero.",
                AcaoSimulada.SemRede => $"Máquina {p.Maquina.Numero}: rede caiu (o WISE continua contando).",
                _ => $"Máquina {p.Maquina.Numero}: {Cenario(acao)}.",
            } + (saiuDoAuto ? " Paradas automáticas desligadas (\"auto\" religa)." : ""));

            if (publica)
                await Publicar(execucao, p, "mudança", cancellationToken);
        }
    }

    /// <summary>
    /// Zera os contadores de uma máquina simulada pelo IP (o que o SmartLine faz por HTTP no WISE
    /// real). Falso se o IP não é de uma máquina simulada ligada.
    /// </summary>
    public async Task<bool> ZerarContadoresAsync(string enderecoIp, CancellationToken cancellationToken = default)
    {
        var execucao = _execucao;
        var p = execucao?.Publicadores.FirstOrDefault(p => p.EnderecoOrigem == enderecoIp);
        if (p is null) return false;

        await _trava.WaitAsync(cancellationToken);
        try { p.Maquina.Reiniciar(DateTime.UtcNow); }
        finally { _trava.Release(); }
        Avisar($"Máquina {p.Maquina.Numero} ({enderecoIp}): contadores zerados pelo SmartLine ao iniciar a medição.");
        return true;
    }

    public static CenarioSimulado Cenario(AcaoSimulada acao) => acao switch
    {
        AcaoSimulada.Rodando => CenarioSimulado.Rodando,
        AcaoSimulada.Falta => CenarioSimulado.FaltaGarrafas,
        AcaoSimulada.Minimo => CenarioSimulado.AbaixoAcumuloMinimo,
        AcaoSimulada.Saida => CenarioSimulado.SaidaGarrafasBloqueada,
        AcaoSimulada.Caixas => CenarioSimulado.SaidaCaixasBloqueada,
        AcaoSimulada.Parada => CenarioSimulado.ParadaSemCausa,
        AcaoSimulada.Desligar => CenarioSimulado.Desligado,
        _ => throw new ArgumentOutOfRangeException(nameof(acao), "Esta ação não é um cenário."),
    };

    public static string Duracao(TimeSpan t) =>
        t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes} min {t.Seconds:00} s" : $"{Math.Max(0, t.Seconds)} s";

    private void Avisar(string texto) => Mensagem?.Invoke(texto);

    private async Task Publicar(Execucao execucao, PublicadorWiseSimulado p, string motivo, CancellationToken cancellationToken)
    {
        try
        {
            string? payload;
            await _trava.WaitAsync(cancellationToken);
            try { payload = await p.PublicarAsync(DateTime.UtcNow, cancellationToken); }
            finally { _trava.Release(); }

            var linha = payload is null
                ? $"Máquina {p.Maquina.Numero} ({p.EnderecoOrigem}) " + (p.Maquina.Falha switch
                {
                    FalhaComunicacao.SemRede => $"sem rede (continua contando: S2={p.Maquina.ContadorProducao})",
                    FalhaComunicacao.SemEnergia => "WISE sem energia (não conta; volta zerado)",
                    _ => "desligada",
                })
                : $"Máquina {p.Maquina.Numero} ({p.EnderecoOrigem}) {motivo}: {p.Maquina.Cenario}, S2={p.Maquina.ContadorProducao} S3={p.Maquina.ContadorRejeito}";
            // A linha de cada intervalo vai para quem quiser acompanhar (console); as demais são avisos.
            if (motivo == "periódica") Publicacao?.Invoke(linha);
            else Avisar(linha);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Avisar($"Máquina {p.Maquina.Numero}: não publicou ({ex.GetBaseException().Message}). O broker está no ar?");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DesligarAsync();
        _trava.Dispose();
    }

    /// <summary>Uma "ligada" do simulador: máquinas, relógios e conexões, até desligar.</summary>
    private sealed class Execucao
    {
        private readonly SimuladorWise _dono;
        private readonly SemaphoreSlim _trava;
        private readonly CancellationTokenSource _cancelamento = new();
        private Task _periodica = Task.CompletedTask;
        private Task _automatica = Task.CompletedTask;

        public OpcoesSimulador Opcoes { get; }
        public DateTime LigadoEmUtc { get; } = DateTime.UtcNow;
        public ParadasAleatorias Automatico { get; }
        public List<PublicadorWiseSimulado> Publicadores { get; } = [];
        public List<ServidorHttpWise> ServidoresHttp { get; } = [];

        public Execucao(SimuladorWise dono, OpcoesSimulador opcoes, SemaphoreSlim trava)
        {
            _dono = dono;
            _trava = trava;
            Opcoes = opcoes;
            Automatico = new ParadasAleatorias(opcoes.Paradas ?? OpcoesParadasAleatorias.Padrao);

            var agora = DateTime.UtcNow;
            var primeiroIp = Math.Clamp(opcoes.PrimeiroIp, 2, 250 - opcoes.Maquinas);
            for (var n = 1; n <= opcoes.Maquinas; n++)
            {
                var maquina = new MaquinaSimulada(n, opcoes.PulsosPorMinutoDa(n), opcoes.RejeitoPercentual, agora);
                Publicadores.Add(new PublicadorWiseSimulado(maquina, $"127.0.0.{primeiroIp + n - 1}", opcoes.Broker, opcoes.PortaBroker));
                if (opcoes.Automatico)
                    Automatico.Ligar(maquina, agora);
            }
        }

        public void Iniciar()
        {
            if (Opcoes.PortaHttp > 0)
            {
                foreach (var p in Publicadores)
                {
                    try
                    {
                        ServidoresHttp.Add(new ServidorHttpWise(p.Maquina, _trava, IPAddress.Parse(p.EnderecoOrigem), Opcoes.PortaHttp));
                    }
                    catch (Exception ex)
                    {
                        _dono.Avisar($"Máquina {p.Maquina.Numero}: interface HTTP não subiu em {p.EnderecoOrigem}:{Opcoes.PortaHttp} ({ex.GetBaseException().Message}). " +
                                     "Outro programa usa a porta? Use outra porta aqui e em WISE_HTTP_PORTA.");
                    }
                }
            }

            _periodica = Task.Run(PeriodicaAsync);
            _automatica = Task.Run(AutomaticaAsync);
        }

        // Publicação periódica (contadores), como o WISE configurado com intervalo fixo.
        private async Task PeriodicaAsync()
        {
            try
            {
                using var relogio = new PeriodicTimer(TimeSpan.FromSeconds(Opcoes.IntervaloSegundos));
                while (await relogio.WaitForNextTickAsync(_cancelamento.Token))
                    foreach (var p in Publicadores)
                        await _dono.Publicar(this, p, "periódica", _cancelamento.Token);
            }
            catch (OperationCanceledException) { }
        }

        // Paradas e quedas de comunicação automáticas: a cada segundo vê se alguma máquina muda;
        // a mudança publica na hora, como o C.O.S. do WISE real.
        private async Task AutomaticaAsync()
        {
            try
            {
                using var relogio = new PeriodicTimer(TimeSpan.FromSeconds(1));
                while (await relogio.WaitForNextTickAsync(_cancelamento.Token))
                {
                    foreach (var p in Publicadores)
                    {
                        CenarioSimulado? novo;
                        MudancaComunicacao? comunicacao;
                        DateTime ate;
                        await _trava.WaitAsync(_cancelamento.Token);
                        try
                        {
                            var agora = DateTime.UtcNow;
                            novo = Automatico.Verificar(p.Maquina, agora);
                            ate = Automatico.ProximaMudanca(p.Maquina) ?? agora;
                            comunicacao = Automatico.VerificarComunicacao(p.Maquina, agora);
                        }
                        finally { _trava.Release(); }

                        if (novo is { } cenario)
                        {
                            _dono.Avisar(cenario == CenarioSimulado.Rodando
                                ? $"Máquina {p.Maquina.Numero}: volta a produzir (próxima parada em ~{Duracao(ate - DateTime.UtcNow)})"
                                : $"Máquina {p.Maquina.Numero}: parada automática {cenario} por {Duracao(ate - DateTime.UtcNow)}");
                            await _dono.Publicar(this, p, "auto", _cancelamento.Token);
                        }

                        if (comunicacao is { } mudanca)
                        {
                            var duracao = Duracao(mudanca.Ate - DateTime.UtcNow);
                            _dono.Avisar(mudanca.Falha switch
                            {
                                FalhaComunicacao.SemRede => $"Máquina {p.Maquina.Numero}: rede caiu por {duracao} (o WISE continua contando)",
                                FalhaComunicacao.SemEnergia => $"Máquina {p.Maquina.Numero}: WISE sem energia por {duracao} (volta com os contadores zerados)",
                                _ => $"Máquina {p.Maquina.Numero}: comunicação voltou (próxima queda em ~{duracao})",
                            });
                            await _dono.Publicar(this, p, "auto", _cancelamento.Token);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        public async Task PararAsync()
        {
            _cancelamento.Cancel();
            try { await Task.WhenAll(_periodica, _automatica); } catch (OperationCanceledException) { }
            foreach (var s in ServidoresHttp) s.Dispose();
            foreach (var p in Publicadores) p.Dispose();
            _cancelamento.Dispose();
        }
    }
}
