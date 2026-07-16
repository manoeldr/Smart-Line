# SmartLine

**Sistema de monitoramento e auditoria de linhas de produção industrial.**

SmartLine permite que indústrias acompanhem em tempo real o desempenho de suas linhas de produção, registrem paradas, calculem OEE (Overall Equipment Effectiveness) e gerem histórico de medições — tudo em uma interface web moderna e responsiva.

---

## Funcionalidades

### Monitoramento em tempo real
- Overview de todas as linhas e máquinas do cliente com status ao vivo (Rodando, Parada Interna, Parada Externa, Parada Planejada)
- Atualização automática a cada 30 segundos
- Indicador de OEE por máquina em sessão ativa

### Medição de produção
- Seleção de linha e máquina para iniciar uma sessão de medição
- Cronômetro de medição com controles de Marcha, Parada e Pausa
- Registro de paradas com motivo (Interna, Externa ou Planejada)
- Cadastro de novos motivos de parada diretamente no modal
- Motivos de pausa planejada compartilhados entre todas as máquinas da linha
- Leituras de produção horárias com confirmação individual
- Estado da medição persistido — ao sair e retornar, a sessão continua de onde parou

### Cálculo de OEE
- Disponibilidade, Performance e Qualidade calculados no backend
- Paradas planejadas descontadas do tempo disponível
- Paradas externas registradas separadamente, sem penalizar OEE

### Configurações
- CRUD completo de Clientes, Linhas, Máquinas e Usuários
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
│   ├── SmartLine.API/          # Controllers, Program.cs
│   ├── SmartLine.Core/         # Entidades, Interfaces, Serviços, Enums
│   ├── SmartLine.Infrastructure/  # EF Core, Migrations, Repositórios
│   └── SmartLine.Tests/        # Testes
├── frontend/
│   └── src/
│       ├── components/         # Layout, Sidebar, Topbar
│       ├── contexts/           # AuthContext, ThemeContext
│       ├── modals/             # Modais de parada, pausa, filtros
│       ├── pages/              # Overview, Medição, Configurações, Login
│       ├── services/           # Clientes HTTP por domínio
│       └── types/              # Tipos TypeScript
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

- [ ] Campos de coleta dinâmicos por máquina (temperatura, pressão, etc.)
- [ ] Seleção de campos a coletar ao iniciar medição
- [ ] Dashboard de OEE por sessão
- [ ] Leitura final ao encerrar medição
- [ ] PWA (Progressive Web App)
- [ ] Deploy em produção

---

## Licença

Todos os direitos reservados. Este software é proprietário e não pode ser copiado, modificado ou distribuído sem autorização expressa do autor.

© 2026 Manoel Rodrigues. Todos os direitos reservados.
