using System.Globalization;
using SmartLine.Iot.Simulacao;

// Simulador de WISE-4051 para testar o Modo Semi Automático sem hardware.
//
// Uso (com o backend rodando):
//   dotnet run --project SmartLine.SimuladorWise -- maquinas=2 intervalo=20 ritmo=600
//
// Parâmetros (todos opcionais):
//   maquinas=2           quantas máquinas simular (1 a 50)
//   intervalo=20         segundos entre publicações periódicas dos contadores
//   ritmo=600            pulsos por minuto no contador de produção (S2) rodando
//   rejeito=1            rejeito (S3) em % da produção
//   broker=127.0.0.1     IP do PC com o SmartLine
//   porta=1883           porta do broker
//   primeiroIp=21        máquina 1 sai por 127.0.0.21, a 2 por 127.0.0.22...
//   auto=sim             paradas aleatórias automáticas em todas as máquinas (auto=nao: só por comando)
//   rodando=10           minutos médios produzindo entre uma parada e outra (sorteado entre 50% e 150%)
//   paradaMin=1          duração mínima de uma parada, em minutos
//   paradaMax=5          duração máxima de uma parada, em minutos
//   desligar=sim         se o WISE sem energia (sem comunicação) entra no sorteio

var parametros = args
    .Select(a => a.Split('=', 2))
    .Where(p => p.Length == 2)
    .ToDictionary(p => p[0].Trim().ToLowerInvariant(), p => p[1].Trim());

int Inteiro(string nome, int padrao) => parametros.TryGetValue(nome, out var v) && int.TryParse(v, out var n) ? n : padrao;
double Real(string nome, double padrao) =>
    parametros.TryGetValue(nome, out var v) && double.TryParse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : padrao;

var quantidade = Math.Clamp(Inteiro("maquinas", 2), 1, 50);
var intervalo = TimeSpan.FromSeconds(Math.Clamp(Inteiro("intervalo", 20), 1, 3600));
var ritmo = Real("ritmo", 600);
var rejeito = Real("rejeito", 1);
var broker = parametros.GetValueOrDefault("broker", "127.0.0.1");
var porta = Inteiro("porta", 1883);
var primeiroIp = Math.Clamp(Inteiro("primeiroip", 21), 2, 250 - quantidade);
bool SimNao(string nome, bool padrao) =>
    parametros.TryGetValue(nome, out var v) ? v is "sim" or "s" or "true" or "1" : padrao;
var autoNoInicio = SimNao("auto", true);
var opcoesAuto = new OpcoesParadasAleatorias(
    TimeSpan.FromMinutes(Math.Clamp(Real("rodando", 10), 0.1, 1440)),
    TimeSpan.FromMinutes(Math.Clamp(Real("paradamin", 1), 0.1, 1440)),
    TimeSpan.FromMinutes(Math.Clamp(Math.Max(Real("paradamax", 5), Real("paradamin", 1)), 0.1, 1440)),
    SimNao("desligar", true));
var automatico = new ParadasAleatorias(opcoesAuto);

var agora = DateTime.UtcNow;
var publicadores = Enumerable.Range(1, quantidade)
    .Select(n => new PublicadorWiseSimulado(
        new MaquinaSimulada(n, ritmo, rejeito, agora),
        $"127.0.0.{primeiroIp + n - 1}", broker, porta))
    .ToList();

if (autoNoInicio)
    foreach (var p in publicadores)
        automatico.Ligar(p.Maquina, agora);

var comandos = new Dictionary<string, CenarioSimulado>
{
    ["rodando"] = CenarioSimulado.Rodando,
    ["falta"] = CenarioSimulado.FaltaGarrafas,
    ["minimo"] = CenarioSimulado.AbaixoAcumuloMinimo,
    ["saida"] = CenarioSimulado.SaidaGarrafasBloqueada,
    ["caixas"] = CenarioSimulado.SaidaCaixasBloqueada,
    ["parada"] = CenarioSimulado.ParadaSemCausa,
    ["desligar"] = CenarioSimulado.Desligado,
};

Console.WriteLine($"""
    ── Simulador de WISE-4051 ─────────────────────────────────────
    Broker: {broker}:{porta} · publicação a cada {intervalo.TotalSeconds:0} s · {ritmo:0} pulsos/min
    Paradas automáticas: {(autoNoInicio ? "ligadas" : "desligadas")} · rodando ~{opcoesAuto.RodandoMedio.TotalMinutes:0.#} min entre paradas · paradas de {opcoesAuto.ParadaMinima.TotalMinutes:0.#} a {opcoesAuto.ParadaMaxima.TotalMinutes:0.#} min{(opcoesAuto.IncluirDesligado ? "" : " · sem desligar")}

    Cadastre no SmartLine cada WISE com o IP abaixo:
    {string.Join(Environment.NewLine, publicadores.Select(p => $"  Máquina {p.Maquina.Numero}: IP {p.EnderecoOrigem}"))}

    Comandos (número da máquina + ação, ou "todas" + ação):
      1 rodando   produzindo, sensores normais
      1 falta     parada por falta de garrafas na entrada (S8)
      1 minimo    parada abaixo do acúmulo mínimo (S1)
      1 saida     parada com saída de garrafas bloqueada (S7)
      1 caixas    parada com saída de caixas bloqueada (S4)
      1 parada    parada sem causa (fica não classificada)
      1 desligar  WISE sem energia (sem comunicação)
      1 reiniciar WISE reinicia: contadores voltam a zero
      1 auto      paradas aleatórias automáticas nesta máquina
      1 manual    só por comando (desliga as automáticas)
      (um comando de parada ou rodando também passa a máquina para manual)
      status      situação de todas
      sair        encerra (Ctrl+C também)
    ───────────────────────────────────────────────────────────────
    """);

// Ctrl+C encerra o processo direto; "sair" encerra fechando as conexões.
using var cancelamento = new CancellationTokenSource();

// Comandos, paradas automáticas e publicação periódica mexem nas mesmas máquinas: um de cada vez.
using var trava = new SemaphoreSlim(1, 1);

async Task<T> ComTrava<T>(Func<T> acao)
{
    await trava.WaitAsync();
    try { return acao(); }
    finally { trava.Release(); }
}

string Duracao(TimeSpan t) => t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes} min {t.Seconds:00} s" : $"{t.Seconds} s";

async Task Publicar(PublicadorWiseSimulado p, string motivo)
{
    try
    {
        string? payload;
        await trava.WaitAsync(cancelamento.Token);
        try { payload = await p.PublicarAsync(DateTime.UtcNow, cancelamento.Token); }
        finally { trava.Release(); }
        var hora = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine(payload is null
            ? $"{hora}  Máquina {p.Maquina.Numero} ({p.EnderecoOrigem}) desligada"
            : $"{hora}  Máquina {p.Maquina.Numero} ({p.EnderecoOrigem}) {motivo,-10} {p.Maquina.Cenario,-22} S2={p.Maquina.ContadorProducao} S3={p.Maquina.ContadorRejeito}");
    }
    catch (OperationCanceledException) when (cancelamento.IsCancellationRequested)
    {
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Máquina {p.Maquina.Numero}: não publicou ({ex.GetBaseException().Message}). O backend está rodando?");
    }
}

// Publicação periódica (contadores), como o WISE configurado com intervalo fixo.
var periodica = Task.Run(async () =>
{
    using var relogio = new PeriodicTimer(intervalo);
    do
    {
        foreach (var p in publicadores)
            await Publicar(p, "periódica");
    }
    while (await relogio.WaitForNextTickAsync(cancelamento.Token).AsTask()
               .ContinueWith(t => t.IsCompletedSuccessfully && t.Result));
});

// Paradas automáticas: a cada segundo vê se alguma máquina deve parar ou voltar; a mudança
// publica na hora, como o C.O.S. do WISE real.
var paradasAutomaticas = Task.Run(async () =>
{
    using var relogio = new PeriodicTimer(TimeSpan.FromSeconds(1));
    while (await relogio.WaitForNextTickAsync(cancelamento.Token).AsTask()
               .ContinueWith(t => t.IsCompletedSuccessfully && t.Result))
    {
        foreach (var p in publicadores)
        {
            var mudou = await ComTrava(() =>
            {
                var agoraUtc = DateTime.UtcNow;
                var novo = automatico.Verificar(p.Maquina, agoraUtc);
                return novo is null ? null : new { Cenario = novo.Value, Ate = automatico.ProximaMudanca(p.Maquina)!.Value - agoraUtc };
            });
            if (mudou is null) continue;

            Console.WriteLine(mudou.Cenario == CenarioSimulado.Rodando
                ? $"{DateTime.Now:HH:mm:ss}  Máquina {p.Maquina.Numero}: volta a produzir (próxima parada em ~{Duracao(mudou.Ate)})"
                : $"{DateTime.Now:HH:mm:ss}  Máquina {p.Maquina.Numero}: parada automática {mudou.Cenario} por {Duracao(mudou.Ate)}");
            await Publicar(p, "auto");
        }
    }
});

// Comandos: cada mudança publica na hora, como o C.O.S. (mudança de estado) do WISE real.
while (true)
{
    var linha = Console.ReadLine();
    if (linha is null) break;

    var partes = linha.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (partes.Length == 0) continue;

    if (partes[0] == "sair") break;

    if (partes[0] == "status")
    {
        foreach (var p in publicadores)
        {
            var proxima = automatico.ProximaMudanca(p.Maquina);
            var modo = proxima is null
                ? "manual"
                : $"auto, {(p.Maquina.Cenario == CenarioSimulado.Rodando ? "para" : "volta")} em {Duracao(proxima.Value - DateTime.UtcNow)}";
            Console.WriteLine($"  Máquina {p.Maquina.Numero} ({p.EnderecoOrigem}): {p.Maquina.Cenario}, S2={p.Maquina.ContadorProducao}, conectada={p.Conectado}, {modo}");
        }
        continue;
    }

    if (partes.Length != 2)
    {
        Console.WriteLine("Formato: <número da máquina ou 'todas'> <ação>. Ex.: 1 falta");
        continue;
    }

    List<PublicadorWiseSimulado> alvos;
    if (partes[0] == "todas")
    {
        alvos = publicadores;
    }
    else if (int.TryParse(partes[0], out var numero) && numero >= 1 && numero <= publicadores.Count)
    {
        alvos = [publicadores[numero - 1]];
    }
    else
    {
        Console.WriteLine($"Máquina '{partes[0]}' não existe (1 a {publicadores.Count}).");
        continue;
    }

    foreach (var p in alvos)
    {
        if (partes[1] is "auto" or "manual")
        {
            await ComTrava(() =>
            {
                if (partes[1] == "auto") automatico.Ligar(p.Maquina, DateTime.UtcNow);
                else automatico.Desligar(p.Maquina);
                return true;
            });
            Console.WriteLine($"  Máquina {p.Maquina.Numero}: {(partes[1] == "auto" ? "paradas automáticas ligadas" : "só por comando")}.");
            continue;
        }

        if (partes[1] != "reiniciar" && !comandos.ContainsKey(partes[1]))
        {
            Console.WriteLine($"Ação '{partes[1]}' desconhecida.");
            break;
        }

        var saiuDoAuto = await ComTrava(() =>
        {
            if (partes[1] == "reiniciar")
            {
                p.Maquina.Reiniciar(DateTime.UtcNow);
                return false;
            }

            // Comando de cenário: quem manda agora é o usuário, até ele pedir "auto" de novo.
            var estavaNoAuto = automatico.Ligado(p.Maquina);
            automatico.Desligar(p.Maquina);
            p.Maquina.MudarCenario(comandos[partes[1]], DateTime.UtcNow);
            return estavaNoAuto;
        });
        if (saiuDoAuto)
            Console.WriteLine($"  Máquina {p.Maquina.Numero}: paradas automáticas desligadas (\"{p.Maquina.Numero} auto\" religa).");

        await Publicar(p, "mudança");
    }
}

cancelamento.Cancel();
await periodica;
await paradasAutomaticas;
foreach (var p in publicadores)
    p.Dispose();
Console.WriteLine("Simulador encerrado.");
