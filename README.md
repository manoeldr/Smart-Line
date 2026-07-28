# SmartLine

**Sistema de monitoramento e auditoria de linhas de produção industrial.**

SmartLine permite que indústrias acompanhem em tempo real o desempenho de suas linhas de produção, registrem paradas, calculem OEE (Overall Equipment Effectiveness) e gerem histórico de medições — tudo em uma interface web moderna e responsiva.

---

## Modos de Medição

SmartLine suporta três modos de coleta de dados, adaptáveis à realidade de cada planta industrial:

### Manual ✅ Completo
O auditor acompanha a linha presencialmente e registra as informações diretamente no sistema. Não requer nenhum hardware adicional.

- Configuração da medição antes de iniciar: forma de coleta, velocidade nominal, sobre velocidade (herdadas da configuração da linha, mas ajustáveis), previsão de término e seleção de campos extras a coletar
- Leituras de produção inseridas a cada hora pelo auditor, com campos extras dinâmicos por máquina
- Registro de paradas com motivo (interna, externa ou planejada)
- Cadastro de novos motivos de parada diretamente no modal
- Motivos de pausa planejada compartilhados entre todas as máquinas da linha
- Contador regressivo de 5 minutos ao atingir a previsão de término, com opção de estender ou finalizar — funciona globalmente, em qualquer tela do sistema
- Leitura final obrigatória (produção + campos extras) ao encerrar a medição manualmente
- Estado da medição persistido — ao sair e retornar, a sessão continua de onde parou

### Semi Automático 🚧 Planejado
Integração com dispositivos IoT instalados nas máquinas para coleta parcialmente automatizada.

- Coleta via dispositivos IoT (ex: WISE-4051) com envio a cada 5 segundos
- Agregação em memória com persistência horária no banco
- Atualização do overview em tempo real via WebSocket

### Automático 🚧 Planejado
Integração direta com o protocolo de comunicação da máquina.

- Conexão direta com o CLP/controlador da máquina
- Coleta contínua sem necessidade de auditor presente

---

## Funcionalidades

### Monitoramento em tempo real
- Overview de todas as linhas e máquinas do cliente com status ao vivo
- Quando não há sessão ativa, mostra automaticamente os dados da última sessão finalizada (com indicação visual diferenciada)
- Atualização automática a cada 30 segundos
- Indicador de OEE por máquina

### Medição de produção
- Cronômetro de medição com controles de Marcha, Parada e Pausa
- Cronômetro congela corretamente durante pausas planejadas
- Layout de duas colunas: controles à esquerda, leituras à direita
- Campos de coleta dinâmicos por máquina (temperatura, pressão, refugo, etc.), configuráveis em Configurações → Máquinas
- Sessão registra o histórico de velocidade nominal, sobre velocidade e campos coletados

### Dashboard
- Tela dedicada de indicadores, com seleção de linha e período de datas
- Cards por máquina com OEE agregado, Disponibilidade, Performance, Qualidade, Produção, Refugo, tempo rodando/parado e número de sessões no período
- Modal de detalhes ao clicar numa máquina: métricas da última sessão (ativa ou finalizada) incluindo OEE, Eficiência, Disponibilidade, Qualidade, MTTR e MTBF
- Gráfico dinâmico por hora, combinando barra de Produção com linhas de campos extras selecionáveis
- Linha do tempo (log cronológico) dos eventos de Marcha e Parada da sessão

### Cálculo de OEE
- Disponibilidade, Performance e Qualidade calculados no backend
- Paradas planejadas descontadas do tempo disponível
- Paradas externas registradas separadamente, sem penalizar OEE
- MTTR (tempo médio de reparo) e MTBF (tempo médio entre falhas) calculados a partir das paradas não planejadas

### Configurações
- CRUD completo de Clientes, Usuários e Máquinas
- Gestão de Linhas movida para dentro do modal de edição do Cliente — cadastro de linhas com nome e máquinas associadas
- Reordenação de máquinas dentro de uma linha via drag and drop
- Padrão de "alterações pendentes": todas as ações dentro de um modal (criar linha, adicionar/remover máquina, reordenar, criar campo, criar motivo) só são persistidas ao clicar em Salvar
- Velocidade nominal e sobre velocidade configuradas pelo Administrador por máquina/linha
- Gestão de campos de coleta e motivos de parada por máquina
- Controle de acesso por nível: SuperAdmin, Auditor e Visualizador
  - Auditor tem uma visão restrita de Clientes, focada apenas na gestão de Linhas

### Exportação e Importação de dados
- Gera um pacote `.zip` (dados.json + fotos) com as categorias selecionadas: Clientes+Linhas, Máquinas, Sessões/Medições e Usuários
- Importação com prévia — mostra a contagem de registros de cada categoria antes de confirmar
- Upsert por ID: dados existentes são atualizados, novos são criados, nada é duplicado
- Pensado para um fluxo de PC central + estações satélite: a configuração (clientes, linhas, máquinas) é definida uma vez e distribuída, garantindo nomes consistentes entre todos os computadores; os dados de medição de cada estação podem ser consolidados de volta no PC central
- Estrutura de pastas de fotos já preparada (`ImagesStopReason/{Cliente}_{Estado}_{Linha}/`), com campo `FotoPath` no banco — aguardando a implementação da captura de foto na tela de Medição

---

## Stack

| Camada | Tecnologia |
|--------|-----------|
| Backend | ASP.NET Core 10 + EF Core 10 |
| Banco de dados | PostgreSQL 16 (Docker) |
| Frontend | React 19 + Vite + TypeScript + Tailwind CSS v4 |
| Gráficos | Recharts |
| Drag and drop | @dnd-kit |
| Autenticação | JWT |
| IDE Backend | JetBrains Rider |
| IDE Frontend | VS Code |

---

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Ferramenta `dotnet-ef` instalada globalmente: `dotnet tool install --global dotnet-ef`

---

## Instalação e execução

### 1. Clone o repositório

```bash
git clone https://github.com/manoeldr/Smart-Line.git
cd Smart-Line
```

### 2. Configure as variáveis de ambiente

Crie um arquivo `.env` na raiz do projeto:

```env
DATABASE_URL=Host=localhost;Port=5432;Database=smartline;Username=smartline;Password=smartline123
JWT_SECRET=sua_chave_secreta_aqui
```

### 3. Suba o banco de dados

```bash
docker compose up -d
```

### 4. Execute as migrations

```bash
cd backend
dotnet ef database update --project SmartLine.Infrastructure/SmartLine.Infrastructure.csproj --startup-project SmartLine.API/SmartLine.API.csproj
```

### 5. Inicie o backend

```bash
dotnet run --project SmartLine.API/SmartLine.API.csproj
```

O backend estará disponível em `http://localhost:5278`.

### 6. Inicie o frontend

```bash
cd ../frontend
npm install
npm run dev
```

O frontend estará disponível em `http://localhost:5173`.

> **Nota (Windows):** se o `global.json` exigir uma versão específica do SDK que você não tem instalada, ajuste o campo `version` para a versão disponível e adicione `"rollForward": "latestMinor"`.

---

## Estrutura do projeto

```
SmartLine/
├── backend/
│ ├── SmartLine.API/ # Controllers, Program.cs
│ ├── SmartLine.Core/ # Entidades, Interfaces, Serviços, Enums
│ ├── SmartLine.Infrastructure/ # EF Core, Migrations, Repositórios
│ └── SmartLine.Tests/ # Testes
├── frontend/
│ └── src/
│ ├── components/
│ │ ├── layout/ # Layout, Sidebar, Topbar
│ │ └── SessaoGlobal/ # Watcher — monitora sessão ativa em qualquer tela
│ ├── contexts/ # AuthContext, ThemeContext
│ ├── modals/ # Modais de parada, pausa, configuração, detalhes
│ ├── pages/ # Overview, Medição, Dashboard, Configurações, Login
│ ├── services/ # Clientes HTTP por domínio
│ ├── styles/ # Classes Tailwind centralizadas (buttons, inputs, badges, modals, cards, tables)
│ └── types/ # Tipos TypeScript
├── docker-compose.yml
├── .env
├── build.sh
└── clean.sh
```

---

## Níveis de acesso

| Nível | Overview | Medição | Dashboard | Configurações |
|-------|----------|---------|-----------|---------------|
| SuperAdmin | ✅ | ✅ | ✅ | Usuários, Clientes (com Linhas), Máquinas, Exportar/Importar |
| Auditor | ✅ | ✅ | ✅ | Clientes (somente gestão de Linhas), Máquinas |
| Visualizador | ✅ | ❌ | ❌ | ❌ |

---

## Scripts utilitários

```bash
./clean.sh    # Remove arquivos ._ gerados pelo macOS antes de commits (Mac apenas)
./build.sh    # Limpa ._ e executa dotnet build (Mac apenas)
```

No Windows, use `dotnet build` diretamente.

---

## Roadmap

### ✅ Concluído
- Modo Manual completo (medição, paradas, leituras, contador de término)
- Dashboard com métricas agregadas, gráfico dinâmico e linha do tempo
- Sistema de estilos Tailwind centralizado
- Exportação/Importação de dados via `.zip`

### Em aberto
- [ ] Migração PostgreSQL → SQLite + empacotamento self-contained .NET com WebView2, gerando um único `.exe` instalável
- [ ] Reformular níveis de usuário: renomear SuperAdmin → Administrador e Visualizador → Cliente; adicionar nível Desenvolvedor
- [ ] Captura e upload de foto em paradas (estrutura de armazenamento e banco já preparadas)
- [ ] Modo Semi Automático (IoT)
- [ ] Modo Automático (integração direta com máquina)

---

## Licença

Todos os direitos reservados. Este software é proprietário e não pode ser copiado, modificado ou distribuído sem autorização expressa do autor.

© 2026 Manoel Rodrigues. Todos os direitos reservados.