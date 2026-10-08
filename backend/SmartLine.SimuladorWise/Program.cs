using System.Globalization;
using SmartLine.Iot.Simulacao;

// Simulador de WISE-4051 para testar o Modo Semi Automático sem hardware.
// O mesmo simulador roda dentro do backend, na Área do Desenvolvedor; este console
// é só outra forma de ligá-lo (com o backend em outra máquina, por exemplo).
//
// Uso (com o backend rodando):
//   dotnet run --project SmartLine.SimuladorWise -- maquinas=2 intervalo=20 ritmo=600
//
// Parâmetros (todos opcionais):
//   maquinas=2           quantas máquinas simular (1 a 50)
//   intervalo=20         segundos entre publicações periódicas dos contadores
//   ritmo=600            garrafas por minuto que a linha produz rodando (um valor ou um por máquina)
//   pulso=1              garrafas por pulso do contador de cada máquina, um por máquina
//                        (pulso=1,1,1,84: a 4ª conta caixas de 84 e manda ritmo÷84 pulsos/min).
//                        Configure o mesmo número como multiplicador da máquina no SmartLine.
//   rejeito=1            rejeito (S3) em % da produção
//   broker=127.0.0.1     IP do PC com o SmartLine
//   porta=1883           porta do broker
//   http=80              porta da interface HTTP de cada WISE simulado (o SmartLine zera os
//                        contadores por ela ao iniciar a medição; WISE_HTTP_PORTA no backend
//                        deve ser a mesma). http=0 desliga.
//   primeiroIp=21        máquina 1 sai por 127.0.0.21, a 2 por 127.0.0.22...
//   auto=sim             paradas aleatórias automáticas em todas as máquinas (auto=nao: só por comando)
//   rodando=10           minutos médios produzindo entre uma parada e outra (sorteado entre 50% e 150%)
//   paradaMin=1          duração mínima de uma parada, em minutos
//   paradaMax=5          duração máxima de uma parada, em minutos
//   desligar=sim         quedas de comunicação automáticas (rede caindo ou WISE sem energia)
//   quedas=60            minutos médios comunicando entre uma queda e outra (sorteado entre 50% e 150%)
//   quedaMin=3           duração mínima de uma queda, em minutos
//   quedaMax=10          duração máxima de uma queda, em minutos

var parametros = args
    .Select(a => a.Split('=', 2))
    .Where(p => p.Length == 2)
    .ToDictionary(p => p[0].Trim().ToLowerInvariant(), p => p[1].Trim());

int Inteiro(string nome, int padrao) => parametros.TryGetValue(nome, out var v) && int.TryParse(v, out var n) ? n : padrao;
double Real(string nome, double padrao) =>
    parametros.TryGetValue(nome, out var v) && double.TryParse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : padrao;
bool SimNao(string nome, bool padrao) =>
    parametros.TryGetValue(nome, out var v) ? v is "sim" or "s" or "true" or "1" : padrao;
// Listas por máquina (repetem o último valor se forem mais curtas que a quantidade).
List<double> Lista(string nome, double padrao)
{
    var valores = parametros.GetValueOrDefault(nome, "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(v => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : padrao)
        .ToList();
    if (valores.Count == 0) valores.Add(padrao);
    return valores;
}

var opcoes = new OpcoesSimulador(
    Maquinas: Math.Clamp(Inteiro("maquinas", 2), 1, 50),
    IntervaloSegundos: Math.Clamp(Inteiro("intervalo", 20), 1, 3600),
    GarrafasPorMinuto: Lista("ritmo", 600),
    GarrafasPorPulso: Lista("pulso", 1),
    RejeitoPercentual: Real("rejeito", 1),
    Broker: parametros.GetValueOrDefault("broker", "127.0.0.1"),
    PortaBroker: Inteiro("porta", 1883),
    PrimeiroIp: Inteiro("primeiroip", 21),
    Automatico: SimNao("auto", true),
    Paradas: new OpcoesParadasAleatorias(
        TimeSpan.FromMinutes(Math.Clamp(Real("rodando", 10), 0.1, 1440)),
        TimeSpan.FromMinutes(Math.Clamp(Real("paradamin", 1), 0.1, 1440)),
        TimeSpan.FromMinutes(Math.Clamp(Math.Max(Real("paradamax", 5), Real("paradamin", 1)), 0.1, 1440)),
        SimNao("desligar", true),
        TimeSpan.FromMinutes(Math.Clamp(Real("quedas", 60), 0.5, 1440)),
        TimeSpan.FromMinutes(Math.Clamp(Real("quedamin", 3), 0.1, 1440)),
        TimeSpan.FromMinutes(Math.Clamp(Math.Max(Real("quedamax", 10), Real("quedamin", 3)), 0.1, 1440))),
    PortaHttp: Math.Clamp(Inteiro("http", 80), 0, 65535));

await using var simulador = new SimuladorWise();
simulador.Mensagem += texto => Console.WriteLine($"{DateTime.Now:HH:mm:ss}  {texto}");
simulador.Publicacao += texto => Console.WriteLine($"{DateTime.Now:HH:mm:ss}  {texto}");

await simulador.LigarAsync(opcoes);
var situacao = simulador.Situacao();
var paradas = opcoes.Paradas!;

Console.WriteLine($"""
    ── Simulador de WISE-4051 ─────────────────────────────────────
    Broker: {opcoes.Broker}:{opcoes.PortaBroker} · publicação a cada {opcoes.IntervaloSegundos} s
    Produção: {string.Join(" · ", situacao.Select(m => $"máq. {m.Numero}: {m.GarrafasPorMinuto:0} garrafas/min = {opcoes.PulsosPorMinutoDa(m.Numero):0.#} pulsos/min{(m.GarrafasPorPulso > 1 ? $" (multiplicador {m.GarrafasPorPulso:0} no SmartLine)" : "")}"))}
    Paradas automáticas: {(opcoes.Automatico ? "ligadas" : "desligadas")} · rodando ~{paradas.RodandoMedio.TotalMinutes:0.#} min entre paradas · paradas de {paradas.ParadaMinima.TotalMinutes:0.#} a {paradas.ParadaMaxima.TotalMinutes:0.#} min
    Quedas de comunicação automáticas: {(opcoes.Automatico && paradas.IncluirDesligado ? $"~a cada {paradas.EntreQuedas.TotalMinutes:0.#} min, de {paradas.QuedaMin.TotalMinutes:0.#} a {paradas.QuedaMax.TotalMinutes:0.#} min (rede ou WISE sem energia)" : "desligadas")}
    Interface HTTP dos WISE: {(situacao.Any(m => m.InterfaceHttp) ? $"porta {opcoes.PortaHttp} (usuário root, senha 00000000): o SmartLine zera os contadores por ela ao iniciar a medição" : "desligada")}

    Cadastre no SmartLine cada WISE com o IP abaixo:
    {string.Join(Environment.NewLine, situacao.Select(m => $"  Máquina {m.Numero}: IP {m.EnderecoIp}"))}

    Comandos (número da máquina + ação, ou "todas" + ação):
      1 rodando   produzindo, sensores normais
      1 falta     parada por falta de garrafas na entrada (S8)
      1 minimo    parada abaixo do acúmulo mínimo (S1)
      1 saida     parada com saída de garrafas bloqueada (S7)
      1 caixas    parada com saída de caixas bloqueada (S4)
      1 parada    parada sem causa (fica não classificada)
      1 desligar  WISE sem energia (sem comunicação, não conta)
      1 semrede   rede cai: o WISE continua contando, mas não publica
      1 reiniciar WISE reinicia: contadores voltam a zero
      1 auto      paradas e quedas de comunicação automáticas nesta máquina
      1 manual    só por comando (desliga as automáticas)
      (um comando de parada, rodando ou semrede também passa a máquina para manual;
       um comando de parada ou rodando devolve a comunicação)
      status      situação de todas
      sair        encerra (Ctrl+C também)
    ───────────────────────────────────────────────────────────────
    """);

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
        foreach (var m in simulador.Situacao())
        {
            var modo = m.ProximaMudancaUtc is { } proxima
                ? $"auto, {(m.Cenario == CenarioSimulado.Rodando ? "para" : "volta")} em {SimuladorWise.Duracao(proxima - DateTime.UtcNow)}"
                : "manual";
            var falha = m.Falha switch
            {
                FalhaComunicacao.SemRede => ", sem rede (continua contando)",
                FalhaComunicacao.SemEnergia => ", WISE sem energia",
                _ => "",
            };
            Console.WriteLine($"  Máquina {m.Numero} ({m.EnderecoIp}): {m.Cenario}{falha}, S2={m.ContadorProducao}, conectada={m.Conectada}, {modo}");
        }
        continue;
    }

    if (partes.Length != 2)
    {
        Console.WriteLine("Formato: <número da máquina ou 'todas'> <ação>. Ex.: 1 falta");
        continue;
    }

    int numero;
    if (partes[0] == "todas")
        numero = 0;
    else if (!int.TryParse(partes[0], out numero) || numero < 1 || numero > opcoes.Maquinas)
    {
        Console.WriteLine($"Máquina '{partes[0]}' não existe (1 a {opcoes.Maquinas}).");
        continue;
    }

    if (!Enum.TryParse<AcaoSimulada>(partes[1], ignoreCase: true, out var acao))
    {
        Console.WriteLine($"Ação '{partes[1]}' desconhecida.");
        continue;
    }

    await simulador.ComandoAsync(numero, acao);
}

await simulador.DesligarAsync();
Console.WriteLine("Simulador encerrado.");
