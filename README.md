# SmartLine

**Sistema de monitoramento e auditoria de linhas de produção industrial.**

SmartLine permite que indústrias acompanhem em tempo real o desempenho de suas linhas de produção, registrem paradas, calculem OEE (Overall Equipment Effectiveness) e gerem histórico de medições — tudo em uma interface web moderna e responsiva.

---

## Modos de Medição

SmartLine suporta três modos de coleta de dados, adaptáveis à realidade de cada planta industrial:

### Manual
O auditor acompanha a linha presencialmente e registra as informações diretamente no sistema. É o modo mais flexível e não requer nenhum hardware adicional.

- Leituras de produção inseridas a cada hora pelo auditor
- Registro de paradas com motivo (interna, externa ou planejada)
- Campos de coleta configuráveis por máquina (temperatura, pressão, refugo, etc.)
- Velocidade nominal e sobre velocidade informadas no início de cada medição
- Previsão de término com finalização automática ao atingir o horário

### Semi Automático *(em desenvolvimento)*
Integração com dispositivos IoT instalados nas máquinas para coleta parcialmente automatizada. Cada máquina pode ter até 4 placas de sensores, cada uma monitorando um parâmetro diferente (temperatura, vibração, pressão, etc.).

- Coleta via dispositivos IoT com envio a cada 5 segundos
- Agregação em memória com persistência horária no banco
- Atualização do overview em tempo real via WebSocket
- Compatível com sensores de pulso rápido (WISE-4051)

### Automático *(em desenvolvimento)*
Integração direta com o protocolo de comunicação da máquina, eliminando a necessidade de intervenção humana na coleta de dados.

- Conexão direta com o CLP/controlador da máquina
- Coleta contínua sem necessidade de auditor presente
- Protocolo já testado e pronto para refatoração

---

## Funcionalidades

### Monitoramento em tempo real
- Overview de todas as linhas e máquinas do cliente com status ao vivo (Rodando, Parada Interna, Parada Externa, Parada Planejada)
- Atualização automática a cada 30 segundos
- Indicador de OEE por máquina em sessão ativa

### Medição de produção
- Seleção de linha e máquina para iniciar uma sessão de medição
- Configuração de velocidade nominal, sobre velocidade e previsão de término antes de iniciar
- Cronômetro de medição com controles de Marcha, Parada e Pausa
- Registro de paradas com motivo (Interna, Externa ou Planejada)
- Cadastro de novos motivos de parada diretamente no modal
- Motivos de pausa planejada compartilhados entre todas as máquinas da linha
- Leituras de produção horárias com confirmação individual
- Estado da medição persistido — ao sair e retornar, a sessão continua de onde parou
- Finalização automática ao atingir a previsão de término (com janela de 5 minutos para extensão)

### Campos de coleta dinâmicos
- Cada máquina pode ter campos de coleta personalizados além da produção (refugo, temperatura, pressão, etc.)
- Configuração por máquina na tela de Configurações
- Seleção de quais campos coletar ao iniciar cada medição
- Histórico de campos coletados salvo por sessão

### Cálculo de OEE
- Disponibilidade, Performance e Qualidade calculados no backend
- Paradas planejadas descontadas do tempo disponível
- Paradas externas registradas separadamente, sem penalizar OEE
- Velocidade nominal e sobre velocidade registradas por sessão para histórico

### Configurações
- CRUD completo de Clientes, Linhas, Máquinas e Usuários
- Gestão de campos de coleta e motivos de parada por máquina
- Controle de acesso por nível: SuperAdmin, Auditor e Visualizador

---

## Stack

| Camada | Tecnologia |
|--------|-----------|
| Backend | ASP.NET Core 10 + EF Core 10 |
| Banco de dados | PostgreSQL 16 (Docker) |
| Frontend | React 19 + Vite + TypeScript + Tailwind CSS |
| Autenticação | JWT |
| IDE Backend | JetBrains Rider |
| IDE Frontend | VS Code |

---

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

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
DATABASE_URL=Host=localhost;Port=5432;Database=smartline;Username=smartline;Password=smartline
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

---

## Estrutura do projeto

```
SmartLine/
├── backend/
│   ├── SmartLine.API/              # Controllers, Program.cs
│   ├── SmartLine.Core/             # Entidades, Interfaces, Serviços, Enums
│   ├── SmartLine.Infrastructure/   # EF Core, Migrations, Repositórios
│   └── SmartLine.Tests/            # Testes
├── frontend/
│   └── src/
│       ├── components/             # Layout, Sidebar, Topbar
│       ├── contexts/               # AuthContext, ThemeContext
│       ├── modals/                 # Modais de parada, pausa, configuração
│       ├── pages/                  # Overview, Medição, Configurações, Login
│       ├── services/               # Clientes HTTP por domínio
│       └── types/                  # Tipos TypeScript
├── docker-compose.yml
├── .env
├── build.sh
└── clean.sh
```

---

## Níveis de acesso

| Nível | Overview | Medição | Configurações |
|-------|----------|---------|---------------|
| SuperAdmin | ✅ | ✅ | Usuários, Clientes, Linhas, Máquinas |
| Auditor | ✅ | ✅ | Linhas, Máquinas |
| Visualizador | ✅ | ❌ | ❌ |

---

## Scripts utilitários

```bash
./clean.sh    # Remove arquivos ._ gerados pelo macOS antes de commits
./build.sh    # Limpa arquivos ._ e executa dotnet build
```

---

## Roadmap

- [ ] Seleção de campos a coletar ao iniciar medição
- [ ] Tabela dinâmica de leituras na tela de medição
- [ ] Contador regressivo com extensão ou finalização automática
- [ ] Dashboard de OEE por sessão
- [ ] Leitura final ao encerrar medição
- [ ] Modo Semi Automático (IoT)
- [ ] Modo Automático (integração direta com máquina)
- [ ] PWA (Progressive Web App)
- [ ] Deploy em produção

---

## Licença

Todos os direitos reservados. Este software é proprietário e não pode ser copiado, modificado ou distribuído sem autorização expressa do autor.

© 2026 Manoel Rodrigues. Todos os direitos reservados.
