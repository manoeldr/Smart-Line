# SmartLine

**Sistema de monitoramento e auditoria de linhas de produção industrial.**

SmartLine permite que indústrias acompanhem em tempo real o desempenho de suas linhas de produção, registrem paradas, calculem OEE (Overall Equipment Effectiveness) e gerem histórico de medições — tudo em uma interface web moderna e responsiva.

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
- Estado da medição persistido — ao sair e retornar, a sessão continua de onde parou

### Semi Automático 🚧 Planejado
Integração com dispositivos IoT instalados nas máquinas para coleta parcialmente automatizada.

### Automático 🚧 Planejado
Integração direta com o protocolo de comunicação da máquina.

---

## Funcionalidades

### Monitoramento em tempo real
- Overview de todas as linhas e máquinas do cliente com status ao vivo
- Quando não há sessão ativa, mostra automaticamente os dados da última sessão finalizada (com indicação visual diferenciada)
- Atualização automática a cada 30 segundos
- Indicador de OEE por máquina

### Dashboard
- Tela dedicada de indicadores, com seleção de linha e período de datas
- Cards por máquina com OEE agregado, Disponibilidade, Performance, Qualidade, Produção, Refugo, tempo rodando/parado e número de sessões no período
- Modal de detalhes ao clicar numa máquina: métricas da última sessão (ativa ou finalizada) incluindo OEE, Eficiência, Disponibilidade, Qualidade, MTTR e MTBF
- Gráfico dinâmico por hora, combinando barra de Produção com linhas de campos extras selecionáveis
- Linha do tempo (log cronológico) dos eventos de Marcha e Parada da sessão, com ícone para visualizar a foto da parada quando registrada

### Cálculo de OEE
- Disponibilidade, Performance e Qualidade calculados no backend
- Paradas planejadas descontadas do tempo disponível
- Paradas externas registradas separadamente, sem penalizar OEE
- MTTR (tempo médio de reparo) e MTBF (tempo médio entre falhas) calculados a partir das paradas não planejadas

### Configurações
- CRUD completo de Clientes, Usuários e Máquinas
- Gestão de Linhas movida para dentro do modal de edição do Cliente — cadastro de linhas com nome e máquinas associadas
- Reordenação de máquinas dentro de uma linha via drag and drop
- Padrão de "alterações pendentes": todas as ações dentro de um modal só são persistidas ao clicar em Salvar
- Velocidade nominal e sobre velocidade configuradas pelo Administrador por máquina/linha
- Gestão de campos de coleta e motivos de parada por máquina
- Controle de acesso por nível: Administrador, Auditor, Cliente e Desenvolvedor

### Licenciamento
- Cada instalação é ativada por uma chave amarrada ao MAC address da máquina
- Validação offline — não depende de internet
- Sistema totalmente bloqueado (inclusive login) até a ativação
- Ferramenta separada (`SmartLine.LicenseGenerator`) para gerar chaves a partir do MAC address do cliente

### Exportação e Importação de dados
- Gera um pacote `.zip` (dados.json + fotos) com as categorias selecionadas: Clientes+Linhas, Máquinas, Sessões/Medições e Usuários
- Importação com prévia — mostra a contagem de registros de cada categoria antes de confirmar
- Upsert por ID: dados existentes são atualizados, novos são criados, nada é duplicado
- Pensado para um fluxo de PC central + estações satélite: a configuração é definida uma vez e distribuída, garantindo nomes consistentes entre todos os computadores

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
| Licenciamento | HMAC-SHA256 amarrado ao MAC address |
| IDE Backend | JetBrains Rider |
| IDE Frontend | VS Code |

---

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) — apenas para desenvolvimento do frontend

> Não é mais necessário Docker/PostgreSQL — o banco é um arquivo SQLite local, criado automaticamente.

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

### 3. Aplique as migrations

```bash
cd backend
dotnet ef database update --project SmartLine.Infrastructure/SmartLine.Infrastructure.csproj --startup-project SmartLine.API/SmartLine.API.csproj
```

Isso cria o arquivo `smartline.db` automaticamente na pasta de saída do build da API.

### 4. (Opcional) Popule dados de teste

O projeto `backend/Seed` insere um cliente, máquinas, linha e usuário admin de exemplo:

```bash
cd backend/Seed
dotnet run
```

### 5. Inicie o backend

```bash
cd backend
dotnet run --project SmartLine.API/SmartLine.API.csproj
```

O backend estará disponível em `http://localhost:5278`.

### 6. Inicie o frontend (modo desenvolvimento, com hot reload)

```bash
cd frontend
npm install
npm run dev
```

O frontend estará disponível em `http://localhost:5173`, com proxy automático para a API.

### 7. Ativação da licença

No primeiro acesso, o sistema pede uma chave de ativação amarrada ao MAC address da máquina. Gere a chave com:

```bash
cd backend/SmartLine.LicenseGenerator
dotnet run -- <MAC_ADDRESS>
```

---

## Build de produção (frontend servido pelo backend)

Para gerar uma versão onde o backend serve tudo num único processo/porta (necessário para o empacotamento final em `.exe`):

```bash
cd frontend
npm run build
```

Depois copie o resultado para dentro do backend:

```bash
# Windows PowerShell
Remove-Item "backend\SmartLine.API\wwwroot" -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item "frontend\dist" "backend\SmartLine.API\wwwroot" -Recurse
```

Suba só o backend — a aplicação completa fica disponível em `http://localhost:5278`, sem precisar do frontend rodando separadamente:

```bash
cd backend
dotnet run --project SmartLine.API/SmartLine.API.csproj
```

> **Nota (Windows):** se o `global.json` exigir uma versão específica do SDK que você não tem instalada, ajuste o campo `version` para a versão disponível e adicione `"rollForward": "latestMinor"`.

---

## Estrutura do projeto
```bash
SmartLine/
├── backend/
│ ├── SmartLine.API/ # Controllers, Program.cs, wwwroot (frontend buildado)
│ ├── SmartLine.Core/ # Entidades, Interfaces, Serviços, Enums
│ ├── SmartLine.Infrastructure/ # EF Core, Migrations, Repositórios
│ ├── SmartLine.LicenseGenerator/ # Ferramenta CLI para gerar chaves de licença
│ ├── Seed/ # Popula dados de teste no banco
│ └── SmartLine.Tests/ # Testes
├── frontend/
│ └── src/
│ ├── components/
│ │ ├── layout/ # Layout, Sidebar, Topbar
│ │ └── SessaoGlobal/ # Watcher — monitora sessão ativa em qualquer tela
│ ├── contexts/ # AuthContext, ThemeContext
│ ├── modals/ # Modais de parada, pausa, configuração, detalhes
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
- Modo Manual completo (medição, paradas, leituras, contador de término)
- Captura e visualização de foto de parada
- Dashboard com métricas agregadas, gráfico dinâmico e linha do tempo
- Sistema de estilos Tailwind centralizado
- Exportação/Importação de dados via `.zip`
- Sistema de licença amarrado ao MAC address
- Reformulação de níveis de usuário
- Migração de PostgreSQL para SQLite
- Backend servindo o frontend buildado (processo único)

### Em aberto
- [ ] Empacotamento self-contained (`.exe` único via `dotnet publish`)
- [ ] Janela nativa com WebView2 (experiência de app desktop, sem abrir navegador)
- [ ] Modo Semi Automático (IoT)
- [ ] Modo Automático (integração direta com máquina)

---

## Licença

Todos os direitos reservados. Este software é proprietário e não pode ser copiado, modificado ou distribuído sem autorização expressa do autor.

© 2026 Manoel Rodrigues. Todos os direitos reservados.