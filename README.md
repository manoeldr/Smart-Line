# SmartLine

**Sistema de monitoramento e auditoria de linhas de produção industrial.**

SmartLine permite que indústrias acompanhem em tempo real o desempenho de suas linhas de produção, registrem paradas, calculem OEE (Overall Equipment Effectiveness) e gerem histórico de medições — tudo em uma interface desktop moderna e responsiva.

---

## Modos de Medição

SmartLine suporta três modos de coleta de dados, adaptáveis à realidade de cada planta industrial:

### Manual ✅ Completo
O auditor acompanha a linha presencialmente e registra as informações diretamente no sistema. Não requer nenhum hardware adicional.

- Configuração da medição antes de iniciar: forma de coleta, velocidade nominal, sobre velocidade (herdadas da configuração da linha, mas ajustáveis), produção até então, seleção de campos extras a coletar (com switches) e previsão de término
- Leituras de produção inseridas a cada hora pelo auditor, com campos extras dinâmicos por máquina, sem scroll lateral mesmo com muitos campos
- Registro de paradas com motivo (interna, externa ou planejada)
- Captura de foto da parada direto pela câmera do dispositivo (ou upload de arquivo), organizada automaticamente por Cliente/Linha/Máquina
- Cadastro de novos motivos de parada diretamente no modal
- Motivos de pausa planejada compartilhados entre todas as máquinas da linha
- "Total Parado" acumula o tempo de todas as paradas da sessão
- Contador regressivo de 5 minutos ao atingir a previsão de término, com opção de estender ou finalizar — funciona globalmente, em qualquer tela do sistema
- Leitura final obrigatória (produção + campos extras) ao encerrar a medição manualmente
- Estado da medição persistido — ao sair e retornar, a sessão continua de onde parou, incluindo o tempo da parada atual (recalculado pelo horário real de início, não um contador que zera)
- Modal de motivo de parada só é exibido na transição real de Parada → Rodando, nunca ao clicar Marcha com a máquina já rodando

### Semi Automático (IoT) ✅ Pronto — validado na bancada com o WISE real (13/10)
Cada máquina recebe um módulo **Advantech WISE-4051** que conta a produção e lê os sensores da linha. O WISE publica por **MQTT** num broker embutido no próprio SmartLine (porta 1883), e o sistema transforma essas mensagens em produção, paradas e motivos, sem operador. Testado de ponta a ponta com o simulador de WISE e, em 13/10, com um WISE-4051 real na bancada (conexão, formato das mensagens, entradas, zerar dos contadores e perda de comunicação).

**WISE e medição**
- Cadastro de WISE em Configurações → Dispositivos IoT: IP fixo e nome opcional, com **ping antes de adicionar** (se não responder, avisa e deixa adicionar mesmo assim)
- Ao iniciar a medição, o WISE é escolhido da lista (conectado / desconectado / em uso em outra medição); só inicia com ele conectado e livre. Fica associado à medição até ela ser finalizada
- Mapa fixo das entradas: contadores de produção (S2, S5, S6) e de refugo (S3), cada um com multiplicador opcional (garrafas por pulso); sensores S1, S4, S7 e S8 liga/desliga. **Sem polaridade fixa** (desde 19/10): sensor **ativo** = entrada em 1 e **normal** = em 0, em todos; se a falha é com o sensor ativo ou normal, quem diz é a regra de classificação da máquina
- "Produção até então" informada ao iniciar, como no Manual: a primeira leitura da sessão é o contador da máquina e o WISE soma a partir dele
- Ao iniciar, o SmartLine **zera os contadores do WISE** pela interface HTTP do aparelho (usuário e senha únicos da instalação), sem ninguém precisar abrir o WISE em campo — o mesmo comando da tela do WISE (`PATCH /di_value/slot_0` com `{"DIVal":[{"ClrCnt":1,"Ch":N}]}`, confirmado na bancada em 19/10), conferindo depois se o contador caiu; se não conseguir, a medição começa mesmo assim e a tela avisa
- Diagnóstico por WISE: Validar entradas (valores ao vivo de cada canal) e Ping ("WISE conectado" / "WISE desconectado")

**Coleta**
- Contínua: vira o dia à meia-noite (o novo dia continua de onde o contador parou) e só termina quando alguém finaliza. Várias máquinas em paralelo
- Produção gravada a cada 5 minutos, nos múltiplos do relógio
- Parada detectada quando a produção não anda por um tempo configurável por máquina (Z, padrão 60 s); o início é o instante do último incremento
- **Regras de classificação** por máquina (e personalizáveis por máquina da linha): cada condição diz se o sensor precisa estar **ativo (1)** ou **normal (0)** para a parada ganhar aquele motivo. No bloco da coleta, o chip do sensor fica amarelo quando ele está do lado da falha segundo as regras; o texto segue ativo/normal. **Regras e Z salvos valem na hora** também para as medições em andamento (a parada em curso ainda sem motivo é classificada de novo; uma já classificada mantém a primeira causa) Uma parada é um registro só, com a **primeira causa** identificada, até a máquina voltar a produzir; parada sem causa conta como Interna e pode ser classificada depois, com histórico
- Textos das entradas editáveis por máquina (ex.: S8 = "Falta de garrafas na entrada")
- Backend reiniciado: a coleta é retomada sozinha e o tempo fora do ar vira período sem comunicação

**Perda de comunicação**
- Depois de 90 s sem mensagem do WISE, a máquina fica **sem comunicação**
- O tempo sem comunicação fica **fora do OEE** (não conta como rodando nem como parado)
- Na volta, a diferença do contador (antes da queda × na volta) é a produção feita no período: aparece em **cinza** no gráfico por hora, dividida pelas horas que ficaram fora, e **não entra** em Eficiência nem Qualidade. A Produção total continua sendo o valor do contador
- Linha do tempo com trecho cinza "Sem comunicação" e card do Overview com borda amarela e "Sem comunicação há X min"

### Automático 🚧 Em desenvolvimento
Coleta direta do PLC da máquina, sem operador. A camada de leitura está pronta e testada; falta o serviço de coleta contínua e as telas. A parte comum de coleta automática (eventos, gravação de paradas e produção, virada do dia, períodos sem comunicação) já foi construída e validada no Semi Automático e será reaproveitada.

**Como os dados são lidos.** As máquinas da Sanmartin expõem um bloco de dados (DB) seguindo a nomenclatura do padrão **Weihenstephan** — contadores, estado, modo, programa e código de falha, cada um com o TagId do padrão embutido no nome da variável (`WS_Total_Pallet_50001`, `WS_Cur_State_00300`). O DB é populado por uma FC no próprio PLC.

Não há servidor WS Protocol no PLC: o bloco existe como vocabulário, não como serviço de rede. A coleta é feita por **leitura direta do DB via protocolo S7 (PUT/GET)**, trazendo os 88 bytes de uma vez — o que garante que estado, alarme e contador venham sempre do mesmo instante.

**O que já existe**
- Mapa completo do DB (22 pontos, offsets e tipos extraídos do projeto TIA Portal)
- Parser puro `byte[88]` → `LeituraPlc`, big-endian explícito, com validação de buffer e de timestamp UTC
- `EstadoWs` com os valores reais do PLC — são **flags** (potências de 2), não códigos sequenciais
- `IPlcDataSource`: contrato único que desacopla o coletor da forma de comunicação
- `S7DataSource`: implementação sobre S7netplus
- Dois simuladores (determinístico e contínuo) que permitem rodar e testar o sistema inteiro sem hardware

**O que falta**
- Classificação de estado → parada Interna/Externa (depende também do programa em execução)
- Processamento de leituras consecutivas em produção e paradas (emitindo os mesmos eventos do Semi Automático)
- Reconexão com backoff
- `BackgroundService` de coleta contínua
- Configuração por máquina, endpoints e telas

**Decisões tomadas**
- Sessão diária automática por máquina, fechada e reaberta à meia-noite — reutiliza a entidade `Sessao` para não duplicar o cálculo de OEE já validado
- Período sem comunicação **sai do tempo disponível**, em vez de virar parada Interna — falha de rede não pode derrubar a Disponibilidade
- Velocidade nominal: usa a do PLC quando vier maior que zero, senão cai para a cadastrada no sistema
- **Qualidade fica indisponível (`null`)** no Modo Automático: as máquinas da Sanmartin não contabilizam refugo. O OEE automático é de dois fatores e não deve ser comparado diretamente com o do Manual
- Configuração de conexão por máquina gravada como JSON, para suportar outros protocolos sem migration

---

## Funcionalidades

### Overview (tempo real)
- Todas as linhas e máquinas do cliente com status ao vivo, na ordem configurada em Configurações (drag and drop)
- Cards sempre do mesmo tamanho, em todas as linhas
- Quando não há sessão ativa, mostra os dados da última sessão finalizada (com indicação visual diferenciada)
- Máquina em coleta Semi Automática: etiquetas **Crítica** e **IoT**, situação da coleta (rodando, parada com o motivo, sem comunicação, aguardando o WISE), "N paradas sem classificação" e botão de finalizar
- Botão **Paradas sem classificação**: lista das paradas das coletas automáticas ainda sem motivo, com filtros e classificação direto da lista
- Atualização automática e silenciosa a cada 30 segundos
- Filtro por data no topo, com **bolinha azul nos dias que tiveram sessão**

### Dashboard
- Seleção de linha; o dia é o do **filtro de data do topo** (sem filtro = hoje, ao vivo), o mesmo do Overview e dos Relatórios
- **Só em medição**: mostra só as máquinas com sessão em andamento (lembrado no navegador; só aparece em hoje). Na visão Linha, os indicadores da linha continuam os da linha inteira; o filtro vale para a tabela e os gráficos por máquina
- Atualiza sozinho logo depois de cada gravação de produção (a cada 5 min), sem recarregar a tela
- **Visão Máquinas**: um card por máquina com os valores exatos de uma sessão — a em andamento ("ao vivo", com a situação agora) ou a última do período: OEE, Disponibilidade, Performance, Qualidade, Produção, Refugo, tempo rodando/parado
- **Visão Linha**: indicadores da linha inteira
  - OEE, Disponibilidade, Eficiência, Qualidade e Produção da **máquina crítica** (a de pior OEE, se houver mais de uma; sem crítica com sessão, a de pior OEE entre as que medem produção)
  - Refugo, tempo parado e número de paradas **somados de todas as máquinas**
  - Gráfico de produção por hora da máquina de referência
  - Paradas de todas as máquinas: por hora (linhas) e por motivo (barras lado a lado), uma cor por máquina; clicar no nome na legenda esconde/mostra a máquina
  - Tabela das máquinas da linha (clicar abre o detalhe)
- A visão escolhida, o gráfico e as máquinas escondidas ficam guardados no navegador

### Detalhe da máquina
- Métricas da sessão (ativa ou finalizada): OEE, Eficiência, Disponibilidade, Qualidade, tempos, Produção, Refugo, MTTR e MTBF — atualizam a cada 5 min
- Bloco da coleta Semi Automática ao vivo (retrátil): situação, WISE, sensores, contadores e finalizar
- Gráfico com seletor **Produção** (por hora, com campos extras; no Semi a hora em andamento aparece tracejada e a produção sem comunicação em cinza), **Paradas por hora** (empilhadas por tipo) e **Paradas por motivo** (Pareto)
- Linha do tempo de Marcha, Parada e Sem comunicação, com data, início, fim e duração (ou "até agora"), editar motivo, histórico de classificação e foto da parada

### Cálculo de OEE
- Disponibilidade, Performance e Qualidade calculados no backend
- Paradas planejadas descontadas do tempo disponível; paradas externas não penalizam Disponibilidade
- Produção Total exibida na tela é sempre a última leitura (o contador); internamente, Performance usa a produção real do turno (última leitura menos a leitura inicial)
- Tempo e produção sem comunicação (Semi Automático) ficam fora do cálculo
- MTTR (tempo médio de reparo) e MTBF (tempo médio entre falhas) calculados a partir das paradas não planejadas
- Fórmulas validadas contra o sistema legado (Python), migrando dados históricos reais e conferindo OEE/MTBF com precisão

### Configurações
- CRUD completo de Clientes, Usuários e Máquinas
- Gestão de Linhas dentro do modal de edição do Cliente — cadastro de linhas com nome e máquinas associadas
- Reordenação de máquinas dentro de uma linha via drag and drop, refletida em Overview, Dashboard e na seleção de máquina da tela de Medição
- Padrão de "alterações pendentes": todas as ações dentro de um modal só são persistidas ao clicar em Salvar
- Confirmações de exclusão usam modal próprio do sistema (não o `confirm()` nativo do navegador)
- Velocidade nominal e sobre velocidade configuradas pelo Administrador por máquina/linha
- Gestão de campos de coleta e motivos de parada por máquina
- Semi Automático por máquina (aba "Semi Automático") e por máquina da linha ("Semi Auto"): textos das entradas, tempo de detecção de parada (Z) e regras de classificação
- Dispositivos IoT: WISE cadastrados e WISE conectados sem cadastro (aviso se o computador não estiver recebendo os WISE)
- Controle de acesso por nível: Administrador, Auditor, Cliente e Desenvolvedor

### Área do Desenvolvedor
Só o nível Desenvolvedor vê o item "Desenvolvedor" no menu, em qualquer instalação:
- **Logs**: as últimas 2.000 linhas do log do backend desde que ele subiu, dentro do sistema, sem terminal — filtro por nível (tudo, avisos e erros, só erros) e por texto, atualização sozinha a cada 2 s, exceções abertas ao clicar. Somem ao reiniciar o backend.
- **Coleta**: situação do broker MQTT (porta, WISE conectados, mensagens processadas, descartadas e perdidas) e cada coleta Semi Automática em andamento como o motor a vê: WISE e conexão, situação, parada aberta, última mensagem, sessão, sensores e contadores brutos. Embaixo, todo WISE que publicou no broker desde que o backend subiu (cadastrado ou não, conectado ou não, identificação, tópico, mensagens, última) — se o IP está aqui, rede e MQTT estão certos.
- **Licença** (só em desenvolvimento): gera a chave de licença e a **senha do Desenvolvedor** de um MAC, como o `SmartLine.LicenseGenerator`, e mostra as placas de rede deste computador. O controller fica dentro de `#if DEBUG` e só responde com `ASPNETCORE_ENVIRONMENT=Development`: o publish de produção (Release) não leva esse código, e a aba some.
- **Simulador de WISE**: os mesmos WISE simulados do console, ligados por dentro do backend — formulário com máquinas, pulsos por minuto (um pulso por unidade; o multiplicador é o da medição), paradas e quedas automáticas; cada máquina com os comandos como botões (rodando, falta, mínimo, saída, caixas, sem causa, sem energia, sem rede, reiniciar, automático/manual). Os IPs simulados (`127.0.0.21`…) aparecem em Dispositivos IoT como aparelhos reais.

### Usabilidade
- O F5 mantém o que estava aberto: modais, abas, item em edição (pela URL), tema escuro e posição da rolagem
- Gráficos sem área de seleção nem moldura de foco ao clicar
- Textos das telas sem termos técnicos para o usuário administrador

### Licenciamento
- Cada instalação é ativada por uma chave amarrada ao MAC address da máquina
- Detecção de MAC estável — prioriza Ethernet físico, ignora adaptadores virtuais (VMware, Hyper-V, VirtualBox, VPN) por prefixo de fabricante (OUI)
- Validação offline — não depende de internet
- Sistema totalmente bloqueado (inclusive login) até a ativação
- Ferramenta separada (`SmartLine.LicenseGenerator`) para gerar chaves a partir do MAC address do cliente

### Exportação e Importação de dados
- Gera um pacote `.zip` (dados.json + fotos) com as categorias selecionadas: Clientes+Linhas (máquinas da linha com Z e regras personalizadas, cadastro de WISE), Máquinas (campos, motivos, textos das entradas e regras do catálogo), Sessões/Medições (produção, paradas com histórico, leituras, trechos sem comunicação) e Usuários
- Importação com prévia — mostra a contagem de registros de cada categoria antes de confirmar
- Upsert por ID: dados existentes são atualizados, novos são criados, nada é duplicado (regras espelham a origem: a que sumiu lá sai aqui; textos de entrada casam por máquina + entrada e WISE pelo IP)
- Pensado para um fluxo de PC central + estações satélite: a configuração é definida uma vez e distribuída, garantindo nomes consistentes entre todos os computadores
- Ferramenta dedicada (`ImportLegacy`) para migrar dados do sistema legado (Python/SQLite) — clientes, linhas, máquinas, motivos de parada e sessões completas com paradas e leituras reconstruídas

### Fotos de parada
- Botão "Tirar foto" na tela de Medição, disponível enquanto a máquina está parada
- Armazenamento organizado em `ImagesStopReason/{Cliente}_{Estado}_{Linha}/{Cliente}_{Estado}_{Linha}_{Máquina}_{DataHora}.jpg`
- Banco guarda apenas o caminho relativo (`FotoPath`), nunca o binário da imagem

---

## Stack

| Camada | Tecnologia |
|--------|-----------|
| Backend | ASP.NET Core 10 + EF Core 10 |
| Banco de dados | SQLite (arquivo local, sem servidor) |
| Frontend | React 19 + Vite + TypeScript + Tailwind CSS v4 |
| Gráficos | Recharts |
| Drag and drop | @dnd-kit |
| Autenticação | JWT |
| IoT (Semi Automático) | MQTTnet — broker MQTT embutido no backend |
| Comunicação com PLC | S7netplus (protocolo S7 / PUT-GET) |
| Testes | xUnit + FakeTimeProvider |
| Licenciamento | HMAC-SHA256 amarrado ao MAC address |
| Empacotamento | Self-contained `.exe` + janela desktop nativa (WinForms + WebView2) |
| IDE Backend | JetBrains Rider |
| IDE Frontend | VS Code |

---

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) — apenas para desenvolvimento do frontend

> Não é necessário Docker/PostgreSQL — o banco é um arquivo SQLite local, criado e migrado automaticamente ao iniciar.

---

## Instalação e execução (desenvolvimento)

### 1. Clone o repositório

```bash
git clone https://github.com/manoeldr/Smart-Line.git
cd Smart-Line
```

### 2. Configure as variáveis de ambiente

Crie um arquivo `.env` na raiz do projeto:

```env
JWT_SECRET=sua_chave_secreta_aqui_com_pelo_menos_32_caracteres

# Semi Automático (opcionais)
MQTT_HABILITADO=true    # false desliga o broker (ex.: estações que não recebem WISE)
MQTT_PORTA=1883
MQTT_LOG_BRUTO=false    # true escreve no log cada mensagem recebida do WISE
WISE_HTTP_PORTA=80      # interface HTTP dos WISE: o SmartLine zera os contadores por ela ao iniciar a medição
WISE_HTTP_USUARIO=root  # usuário e senha de acesso ao WISE (os mesmos da tela web dele), iguais para todos os WISE da instalação
WISE_HTTP_SENHA=00000000
```

### 3. Inicie o backend

As migrations são aplicadas automaticamente ao iniciar — não é necessário rodar `dotnet ef` manualmente.

```bash
cd backend
dotnet run --project SmartLine.API/SmartLine.API.csproj
```

O backend estará disponível em `http://localhost:5278`, com o broker MQTT na porta 1883.

### 4. (Opcional) Popule um usuário administrador

O projeto `backend/Seed` cria o usuário admin (login `admin`, senha `admin123`) usando o próprio Entity Framework Core — nunca insira dados via SQL bruto diretamente no banco, o formato de GUID gerado pelo EF Core é incompatível com texto puro:

```bash
cd backend/Seed
dotnet run
```

Clientes, Linhas e Máquinas devem ser cadastrados pela própria interface (Configurações), o que garante o formato de dados correto.

### 5. Inicie o frontend (modo desenvolvimento, com hot reload)

```bash
cd frontend
npm install
npm run dev
```

O frontend estará disponível em `http://localhost:5173`, com proxy automático para a API.

### 6. (Opcional) Simulador de WISE

Para testar o Semi Automático sem hardware. O caminho mais simples é pela tela: entre como Desenvolvedor, menu **Desenvolvedor → Simulador de WISE**, e ligue por ali (o simulador roda dentro do próprio backend). O console abaixo é o mesmo simulador para quem prefere terminal ou quer rodá-lo em outro computador, com o backend rodando:

```bash
cd backend
dotnet run --project SmartLine.SimuladorWise -- maquinas=4 intervalo=20 ritmo=600
```

Cada máquina simulada publica de um IP próprio (`127.0.0.21`, `.22`…), que deve ser cadastrado em Configurações → Dispositivos IoT. Por padrão as máquinas param sozinhas, em intervalos e por motivos sorteados, e a comunicação cai de vez em quando (rede ou WISE sem energia).

| Parâmetro | Padrão | O que faz |
|-----------|--------|-----------|
| `maquinas` | 2 | Quantos WISE simular (1 a 50) |
| `intervalo` | 20 | Segundos entre publicações |
| `ritmo` | 600 | Garrafas por minuto que a linha produz (um valor ou um por máquina) |
| `pulso` | 1 | Garrafas por pulso do contador, um por máquina: `pulso=1,1,1,84` faz a 4ª mandar ritmo÷84 pulsos/min. Cadastre o mesmo número como multiplicador da máquina no SmartLine |
| `auto` | sim | Paradas e quedas automáticas (`nao`: só por comando) |
| `rodando` / `paradaMin` / `paradaMax` | 10 / 1 / 5 | Minutos médios entre paradas e duração delas |
| `desligar` | sim | Quedas de comunicação automáticas |
| `http` | 80 | Porta da interface HTTP de cada WISE simulado (zerar contadores); `0` desliga |
| `quedas` / `quedaMin` / `quedaMax` | 60 / 3 / 10 | Minutos médios entre quedas e duração delas |

Comandos no console (número da máquina ou `todas` + ação): `rodando`, `falta`, `minimo`, `saida`, `caixas`, `parada`, `desligar`, `semrede`, `reiniciar`, `auto`, `manual`, além de `status` e `sair`.

### 7. Ativação da licença

No primeiro acesso, o sistema pede uma chave de ativação amarrada ao MAC address da máquina. Gere a chave com:

```bash
cd backend/SmartLine.LicenseGenerator
dotnet run -- <MAC_ADDRESS>
```

---

## Build de produção (`.exe` desktop)

### 1. Builda o frontend e copia pro backend

```bash
cd frontend
npm run build
```

```powershell
# Windows PowerShell
Remove-Item "backend\SmartLine.API\wwwroot" -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item "frontend\dist" "backend\SmartLine.API\wwwroot" -Recurse
```

### 2. Publica o backend como self-contained

```powershell
cd backend
dotnet publish SmartLine.API/SmartLine.API.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

### 3. Monta a pasta do app desktop

Copia `SmartLine.API.exe` (de `publish/`) e o `.env` pra dentro da pasta de build do `SmartLine.Desktop` (`bin\Debug\net10.0-windows\` ou `bin\Release\...`), junto com o `wwwroot`. O `SmartLine.Desktop.exe` sobe o backend automaticamente como processo filho (porta 5278 fixa via `ASPNETCORE_URLS`), espera ele responder, e abre a interface numa janela nativa via WebView2. Fechar a janela encerra o backend junto.

Pra distribuir, crie um atalho apontando direto pro `SmartLine.Desktop.exe`.

> **Semi Automático em produção:** o PC que recebe os WISE precisa liberar no firewall a porta 1883 (MQTT), a 5278 (estações) e o ping (ICMP). Em PowerShell como administrador:
>
> ```powershell
> New-NetFirewallRule -DisplayName "Smart Line Scan - MQTT" -Direction Inbound -Protocol TCP -LocalPort 1883 -Action Allow
> New-NetFirewallRule -DisplayName "Smart Line Scan - API" -Direction Inbound -Protocol TCP -LocalPort 5278 -Action Allow
> New-NetFirewallRule -DisplayName "Smart Line Scan - Ping" -Direction Inbound -Protocol ICMPv4 -IcmpType 8 -Action Allow
> ```

### Configuração do WISE-4051 (validada na bancada em 13/10)

Pela tela web do WISE (`http://<ip-do-wise>`, usuário `root`). Os nomes dos canais são só etiqueta (máximo 21 caracteres); o sistema identifica pelo número: **DI0 = S1 … DI7 = S8**.

| Onde | O quê |
|---|---|
| Configuration → Network | IP fixo na mesma rede do PC; a senha de acesso é a que vai em `WISE_HTTP_SENHA` |
| I/O Status / Configuration → DI | DI1, DI2, DI4, DI5 (S2, S3, S5, S6) em modo **Counter**; DI0, DI3, DI6, DI7 (S1, S4, S7, S8) em modo **DI**. **Sem Invert Signal**: o sistema lê 1 = ativo, 0 = normal, e a regra da máquina diz se a falha é com o sensor ativo ou normal. Filtro curto nos contadores, ~10 s nos sensores |
| Configuration → Cloud | Select Service: **iSensing MQTT**. Host: IP do PC. Porta `1883`. SSL e WebSocket: Disable. Usuário e senha: em branco (o broker não autentica). QoS 0, Retain: Disable |
| Advanced → Data Logger → Logger Configuration | **Push Notification → I/O Log** ligado (Memory Storage pode ficar desligado) |
| Advanced → Data Logger → Data Configuration → I/O | **By Period** = `100` (a unidade é 0,1 s → 10 s; o sistema considera sem comunicação aos 90 s). Em **Log Data → DI**: **Log Enabled** nos 8 canais; **Change of State** só nos sensores (S1, S4, S7, S8); **Reset Counter desligado** em todos |

Conferência: em Configurações → Dispositivos IoT o WISE aparece **conectado**; na Área do Desenvolvedor → Logs, com `MQTT_LOG_BRUTO=true`, chega uma linha `Advantech/<MAC>/data` a cada 10 s com `{"s":…,"di1":false,"di2":0,…}` (contadores como número, sensores como true/false); **Validar entradas** mostra os 8 canais; ao iniciar uma medição o log diz "Contadores do WISE … zerados" e o `Val` em `http://<ip>/di_value/slot_0/ch_1` volta a 0.

> **Nota (Windows):** se o `global.json` exigir uma versão específica do SDK que você não tem instalada, ajuste o campo `version` para a versão disponível e adicione `"rollForward": "latestMinor"`.

---

## Estrutura do projeto

```powershell
SmartLine/
├── backend/
│ ├── SmartLine.API/ # Controllers, Program.cs, wwwroot (frontend buildado)
│ ├── SmartLine.Core/ # Entidades, Interfaces, Serviços (OEE), Enums, motor da coleta IoT
│ ├── SmartLine.Iot/ # Broker MQTT, serviço de coleta do Semi Automático, simulação de WISE, log em memória
│ ├── SmartLine.Plc/ # Comunicação com PLC (parser do DB, S7, simuladores)
│ ├── SmartLine.Infrastructure/ # EF Core, Migrations, Repositórios, gravação da coleta
│ ├── SmartLine.SimuladorWise/ # Console do simulador de WISE-4051 (o mesmo que roda dentro do backend)
│ ├── SmartLine.Desktop/ # App desktop (WinForms + WebView2), sobe o backend como processo filho
│ ├── SmartLine.LicenseGenerator/ # Ferramenta CLI para gerar chaves de licença
│ ├── Seed/ # Cria o usuário admin
│ ├── ImportLegacy/ # Migra dados do sistema legado (Python/SQLite)
│ └── SmartLine.Tests/ # Testes (Infra, Iot, Plc, OEE)
├── frontend/
│ └── src/
│ ├── components/
│ │ ├── layout/ # Layout, Sidebar, Topbar
│ │ ├── iot/ # Painel da coleta, WISE da medição, editor de regras
│ │ └── SessaoGlobal/ # Watcher — monitora sessão ativa em qualquer tela
│ ├── contexts/ # AuthContext, ThemeContext
│ ├── hooks/ # Estado na URL (F5), rolagem mantida
│ ├── modals/ # Modais de parada, pausa, configuração, detalhes, WISE, confirmação
│ ├── pages/ # Overview, Medição, Dashboard, Configurações, Login, Ativação
│ ├── services/ # Clientes HTTP por domínio
│ ├── styles/ # Classes Tailwind centralizadas
│ └── types/ # Tipos TypeScript
├── .env
├── build.sh
└── clean.sh
```

---

## Níveis de acesso

| Nível | Overview | Medição | Dashboard | Configurações | Desenvolvedor |
|-------|----------|---------|-----------|---------------|---------------|
| Administrador | ✅ | ✅ | ✅ | Usuários, Clientes (com Linhas), Máquinas, Dispositivos IoT, Exportar/Importar | ❌ |
| Desenvolvedor | ✅ | ✅ | ✅ | Mesmo acesso do Administrador | Logs, coleta (broker e motor) e simulador de WISE |
| Auditor | ✅ | ✅ | ✅ | Clientes (somente gestão de Linhas), Máquinas | ❌ |
| Cliente | ✅ | ❌ | ❌ | ❌ | ❌ |

O perfil **Desenvolvedor é reservado**: o Administrador não vê os usuários Desenvolvedor, não os edita nem dá esse nível a ninguém (o backend recusa, não só a tela), e o pacote de exportação/importação de quem não é Desenvolvedor não leva nem traz Desenvolvedores. A **senha do Desenvolvedor não fica no banco**: é derivada do segredo da licença e do MAC do computador (gerada na aba Licença ou pelo `LicenseGenerator`, junto com a chave), então nem editando o `.db` dá para trocá-la. O backend garante um usuário `desenvolvedor` em toda instalação e, em desenvolvimento, imprime a senha desta máquina no log ao subir.

No Semi Automático, cadastrar WISE, validar entradas e ping são do Administrador e do Desenvolvedor; escolher o WISE, editar regras e textos, iniciar, finalizar e classificar paradas também são do Auditor. O Cliente só visualiza.

---

## Roadmap

### ✅ Concluído
- Modo Manual completo (medição, paradas, leituras, contador de término, persistência de estado robusta)
- Captura e visualização de foto de parada
- Dashboard com métricas, gráficos e linha do tempo
- Sistema de estilos Tailwind centralizado
- Exportação/Importação de dados via `.zip`
- Sistema de licença amarrado ao MAC address, com detecção estável
- Reformulação de níveis de usuário
- Migração completa de PostgreSQL para SQLite
- Backend servindo o frontend buildado (processo único)
- Empacotamento self-contained (`.exe`) e janela nativa com WebView2
- Migração de dados do sistema legado, com fórmulas de OEE/MTBF validadas contra o código original
- **Modo Automático — camada de leitura do PLC**: mapa do DB Weihenstephan, parser puro, contrato `IPlcDataSource`, implementação S7 e simuladores
- **Modo Semi Automático (IoT)**: broker MQTT embutido, cadastro de WISE com ping, coleta contínua com virada do dia e retomada após reinício, regras de classificação por sensor, produção até então, perda de comunicação fora do OEE, simulador de WISE com paradas e quedas automáticas
- **Dashboard — visão Linha**: OEE pela máquina crítica, paradas somadas por máquina, produção por hora da crítica
- F5 mantém modais, abas, tema e rolagem; filtros de data com bolinha nos dias com sessão
- Zerar os contadores do WISE por HTTP ao iniciar a medição; licença aceitando qualquer placa de rede física; filtro por data do Overview mostrando a sessão do dia
- **Área do Desenvolvedor**: logs do backend, situação da coleta e simulador de WISE dentro do sistema, sem terminal
- Exportação/Importação leva regras, textos das entradas, cadastro de WISE, Z, histórico de paradas e trechos sem comunicação
- Filtros de data com um dia só, começando por hoje; tempo de parada (Z) no Adicionar máquina; SQLite nativo atualizado
- **Bancada com o WISE-4051 real (13/10)**: tópico e JSON no formato esperado, contadores e sensores lidos, zerar por HTTP funcionando, perda e retorno de comunicação; guia de configuração do WISE neste README

### Próximo
- [ ] Modo Automático: classificação de estado, processamento de leituras, reconexão, serviço de coleta e telas

### Em aberto
- [ ] Rede/Multi-usuário — IP fixo no PC central, faixa do roteador e liberação das portas no firewall
- [ ] Levantar os valores numéricos de `WS_Cur_Mode_00100` e `WS_Cur_Prog_00200` (Networks 26 e 27 da FC)

---

## Testes

```bash
cd backend
dotnet test SmartLine.Tests/SmartLine.Tests.csproj
```

São 398 testes, todos passando. Para rodar só uma frente:

```bash
dotnet test SmartLine.Tests/SmartLine.Tests.csproj --filter "FullyQualifiedName~Iot"
dotnet test SmartLine.Tests/SmartLine.Tests.csproj --filter "FullyQualifiedName~Plc"
```

> Com o simulador rodando, pare-o antes (ou use sempre o caminho do projeto, como acima): ele trava a DLL compartilhada e o `dotnet build` da solução inteira falha.

Tudo é testável sem hardware:
- **Semi Automático:** o motor da coleta é puro (amostras entram, eventos saem) e o serviço de coleta roda contra um broker MQTT real em memória, com o relógio controlado por `FakeTimeProvider`
- **Automático:** o parser é função pura, e os simuladores implementam o mesmo `IPlcDataSource` que a implementação S7; horas de produção simulada rodam em milissegundos

---

## Licença

Todos os direitos reservados. Este software é proprietário e não pode ser copiado, modificado ou distribuído sem autorização expressa do autor.

© 2026 Manoel Rodrigues. Todos os direitos reservados.
