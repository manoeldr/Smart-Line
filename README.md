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

### Semi Automático 🚧 Planejado
Integração com dispositivos IoT instalados nas máquinas para coleta parcialmente automatizada.

### Automático 🚧 Em desenvolvimento
Coleta direta do PLC da máquina, sem operador. A camada de leitura está pronta e testada; falta o serviço de coleta contínua e as telas.

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
- Processamento de leituras consecutivas em produção e paradas
- Reconexão com backoff
- `BackgroundService` de coleta contínua, com virada de sessão à meia-noite
- Entidades, migration, endpoints e telas

**Decisões tomadas**
- Sessão diária automática por máquina, fechada e reaberta à meia-noite — reutiliza a entidade `Sessao` (com `TipoSessao`) para não duplicar o cálculo de OEE já validado
- Sem "dono" da sessão: a coleta roda no servidor enquanto a máquina estiver habilitada, independente de haver alguém logado
- Período sem comunicação **sai do tempo disponível**, em vez de virar parada Interna — falha de rede não pode derrubar a Disponibilidade
- Velocidade nominal: usa a do PLC quando vier maior que zero, senão cai para a cadastrada no sistema
- **Qualidade fica indisponível (`null`)** no Modo Automático: as máquinas da Sanmartin não contabilizam refugo. O OEE automático é de dois fatores e não deve ser comparado diretamente com o do Manual
- Configuração de conexão por máquina gravada como JSON, para suportar outros protocolos sem migration

---

## Funcionalidades

### Monitoramento em tempo real
- Overview de todas as linhas e máquinas do cliente com status ao vivo
- Quando não há sessão ativa, mostra automaticamente os dados da última sessão finalizada (com indicação visual diferenciada)
- Atualização automática a cada 30 segundos
- Indicador de OEE por máquina
- Máquinas listadas na mesma ordem configurada em Configurações (drag and drop)

### Dashboard
- Tela dedicada de indicadores, com seleção de linha e período de datas
- Cards por máquina com OEE agregado, Disponibilidade, Performance, Qualidade, Produção, Refugo, tempo rodando/parado e número de sessões no período
- Modal de detalhes ao clicar numa máquina: métricas da última sessão (ativa ou finalizada) incluindo OEE, Eficiência, Disponibilidade, Qualidade, MTTR e MTBF — cabeçalho, métricas e gráfico ficam fixos, só a linha do tempo tem scroll próprio
- Gráfico dinâmico por hora, combinando barra de Produção com linhas de campos extras selecionáveis
- Linha do tempo (log cronológico) dos eventos de Marcha e Parada da sessão, com ícone para visualizar a foto da parada quando registrada

### Cálculo de OEE
- Disponibilidade, Performance e Qualidade calculados no backend
- Paradas planejadas descontadas do tempo disponível; paradas externas não penalizam Disponibilidade
- Produção Total exibida na tela é sempre a última leitura apontada (mesmo padrão dos campos de coleta extras); internamente, Performance usa a produção real do turno (última leitura menos a leitura inicial)
- MTTR (tempo médio de reparo) e MTBF (tempo médio entre falhas) calculados a partir das paradas não planejadas
- Fórmulas validadas byte a byte contra o sistema legado (Python), migrando dados históricos reais e conferindo OEE/MTBF com precisão

### Configurações
- CRUD completo de Clientes, Usuários e Máquinas
- Gestão de Linhas movida para dentro do modal de edição do Cliente — cadastro de linhas com nome e máquinas associadas
- Reordenação de máquinas dentro de uma linha via drag and drop, refletida em Overview, Dashboard e na seleção de máquina da tela de Medição
- Padrão de "alterações pendentes": todas as ações dentro de um modal só são persistidas ao clicar em Salvar
- Confirmações de exclusão usam modal próprio do sistema (não o `confirm()` nativo do navegador)
- Todos os modais principais têm botão de fechar (X)
- Velocidade nominal e sobre velocidade configuradas pelo Administrador por máquina/linha
- Gestão de campos de coleta e motivos de parada por máquina
- Controle de acesso por nível: Administrador, Auditor, Cliente e Desenvolvedor

### Licenciamento
- Cada instalação é ativada por uma chave amarrada ao MAC address da máquina
- Detecção de MAC estável — prioriza Ethernet físico, ignora adaptadores virtuais (VMware, Hyper-V, VirtualBox, VPN) por prefixo de fabricante (OUI)
- Validação offline — não depende de internet
- Sistema totalmente bloqueado (inclusive login) até a ativação
- Ferramenta separada (`SmartLine.LicenseGenerator`) para gerar chaves a partir do MAC address do cliente

### Exportação e Importação de dados
- Gera um pacote `.zip` (dados.json + fotos) com as categorias selecionadas: Clientes+Linhas, Máquinas, Sessões/Medições e Usuários
- Importação com prévia — mostra a contagem de registros de cada categoria antes de confirmar
- Upsert por ID: dados existentes são atualizados, novos são criados, nada é duplicado
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
```

### 3. Inicie o backend

As migrations são aplicadas automaticamente ao iniciar — não é necessário rodar `dotnet ef` manualmente.

```bash
cd backend
dotnet run --project SmartLine.API/SmartLine.API.csproj
```

O backend estará disponível em `http://localhost:5278`.

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

### 6. Ativação da licença

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

> **Nota (Windows):** se o `global.json` exigir uma versão específica do SDK que você não tem instalada, ajuste o campo `version` para a versão disponível e adicione `"rollForward": "latestMinor"`.

---

## Estrutura do projeto

```powershell
SmartLine/
├── backend/
│ ├── SmartLine.API/ # Controllers, Program.cs, wwwroot (frontend buildado)
│ ├── SmartLine.Core/ # Entidades, Interfaces, Serviços, Enums
│ ├── SmartLine.Plc/ # Comunicação com PLC (parser do DB, S7, simuladores)
│ ├── SmartLine.Infrastructure/ # EF Core, Migrations, Repositórios
│ ├── SmartLine.Desktop/ # App desktop (WinForms + WebView2), sobe o backend como processo filho
│ ├── SmartLine.LicenseGenerator/ # Ferramenta CLI para gerar chaves de licença
│ ├── Seed/ # Cria o usuário admin
│ ├── ImportLegacy/ # Migra dados do sistema legado (Python/SQLite)
│ └── SmartLine.Tests/ # Testes
├── frontend/
│ └── src/
│ ├── components/
│ │ ├── layout/ # Layout, Sidebar, Topbar
│ │ └── SessaoGlobal/ # Watcher — monitora sessão ativa em qualquer tela
│ ├── contexts/ # AuthContext, ThemeContext
│ ├── modals/ # Modais de parada, pausa, configuração, detalhes, confirmação
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

| Nível | Overview | Medição | Dashboard | Configurações |
|-------|----------|---------|-----------|---------------|
| Administrador | ✅ | ✅ | ✅ | Usuários, Clientes (com Linhas), Máquinas, Exportar/Importar |
| Desenvolvedor | ✅ | ✅ | ✅ | Mesmo acesso do Administrador (+ telas de debug futuras) |
| Auditor | ✅ | ✅ | ✅ | Clientes (somente gestão de Linhas), Máquinas |
| Cliente | ✅ | ❌ | ❌ | ❌ |

---

## Roadmap

### ✅ Concluído
- Modo Manual completo (medição, paradas, leituras, contador de término, persistência de estado robusta)
- Captura e visualização de foto de parada
- Dashboard com métricas agregadas, gráfico dinâmico e linha do tempo
- Sistema de estilos Tailwind centralizado
- Exportação/Importação de dados via `.zip`
- Sistema de licença amarrado ao MAC address, com detecção estável
- Reformulação de níveis de usuário
- Migração completa de PostgreSQL para SQLite
- Backend servindo o frontend buildado (processo único)
- Empacotamento self-contained (`.exe`) e janela nativa com WebView2
- Migração de dados do sistema legado, com fórmulas de OEE/MTBF validadas contra o código original
- Reordenação de máquinas persistida corretamente em todas as telas
- Confirmações de exclusão via modal do sistema, não `confirm()` nativo
- Correção do MTTR — considera apenas paradas Internas, como no sistema legado
- **Modo Automático — camada de leitura do PLC**: mapa do DB Weihenstephan, parser puro, contrato `IPlcDataSource`, implementação S7 e simuladores, com 53 testes

### Em andamento — Modo Automático
- [ ] `ClassificadorEstado` — traduz estado + programa em rodando / parada Interna / Externa / fora do tempo disponível
- [ ] `ProcessadorLeitura` — compara leituras consecutivas e gera produção e eventos de parada (inclui rollover de contador e período sem comunicação)
- [ ] `ResilientDataSource` — reconexão com backoff exponencial e estados Conectado / Reconectando / Offline
- [ ] `ColetorService` (`BackgroundService`) — polling contínuo por máquina e virada de sessão à meia-noite no fuso local
- [ ] Entidades e migration — `TipoSessao`, `UsuarioId` nulo em sessão automática, configuração de coleta por máquina
- [ ] Endpoints e telas — painel ao vivo de coleta e configuração por máquina (Admin)

### Em aberto
- [ ] **Dois testes de OEE falhando** (`Qualidade_ComRefugo` e `Oee_SemParadasSemRefugo`) — anteriores a esta frente, precisam ser investigados: ou o cálculo regrediu, ou os testes ficaram desatualizados
- [ ] Rede/Multi-usuário — IP fixo no PC central, faixa do roteador e liberação da porta 5278 no firewall
- [ ] Modo Semi Automático (IoT)
- [ ] Telas de debug/logs/health check para nível Desenvolvedor
- [ ] Levantar os valores numéricos de `WS_Cur_Mode_00100` e `WS_Cur_Prog_00200` (Networks 26 e 27 da FC)

---

## Testes

```bash
cd backend
dotnet test SmartLine.Tests/SmartLine.Tests.csproj
```

Para rodar só uma frente:

```bash
dotnet test SmartLine.Tests/SmartLine.Tests.csproj --filter "FullyQualifiedName~Plc"
```

A camada de PLC é testável sem hardware: o parser é função pura, e os simuladores implementam o mesmo `IPlcDataSource` que a implementação S7. O simulador contínuo usa `FakeTimeProvider`, então horas de produção simulada rodam em milissegundos.

---

## Licença

Todos os direitos reservados. Este software é proprietário e não pode ser copiado, modificado ou distribuído sem autorização expressa do autor.

© 2026 Manoel Rodrigues. Todos os direitos reservados.