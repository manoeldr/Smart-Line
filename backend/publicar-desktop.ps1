# Gera o pacote do SmartLine Desktop (.exe self-contained) em backend\publish-desktop.
# Rode no PowerShell, na pasta backend:   .\publicar-desktop.ps1          (ou com -Zip)
# Passos: build do frontend -> wwwroot -> publish da API -> publish do Desktop -> monta a pasta.
param(
    [switch]$Zip,            # também gera ..\SmartLine-Pacote-SelfContained.zip
    [switch]$SemFrontend     # pula o npm run build (usa o wwwroot que já está na API)
)
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot        # D:\Smart Line
$backend = $PSScriptRoot
$frontend = Join-Path $raiz 'frontend'
$wwwroot = Join-Path $backend 'SmartLine.API\wwwroot'
$publishApi = Join-Path $backend 'publish'
$destino = Join-Path $backend 'publish-desktop'

# O pacote anterior não pode estar rodando (o backend fica aberto como processo filho mesmo
# depois de fechar a janela): senão a cópia do .exe e do banco falha.
$rodando = Get-Process -Name 'SmartLine.API', 'SmartLine.Desktop' -ErrorAction SilentlyContinue
if ($rodando) {
    Write-Host 'Encerrando o Smart Line Scan que está aberto...' -ForegroundColor Yellow
    $rodando | Stop-Process -Force
    Start-Sleep -Seconds 2
}

# 1. Frontend
if (-not $SemFrontend) {
    Write-Host '== 1/4 Frontend (npm run build)' -ForegroundColor Cyan
    Push-Location $frontend
    npm run build
    if ($LASTEXITCODE -ne 0) { throw 'npm run build falhou' }
    Pop-Location
    Remove-Item $wwwroot -Recurse -Force -ErrorAction SilentlyContinue
    Copy-Item (Join-Path $frontend 'dist') $wwwroot -Recurse
}

# 2. API self-contained, arquivo único
Write-Host '== 2/4 Publish da API' -ForegroundColor Cyan
Remove-Item $publishApi -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish (Join-Path $backend 'SmartLine.API\SmartLine.API.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publishApi
if ($LASTEXITCODE -ne 0) { throw 'publish da API falhou' }

# 3. Desktop self-contained, arquivo único. Antes de apagar a pasta, guarda o que é DADO do
#    pacote e não pode se perder: .env, o banco (smartline.db + -wal/-shm: licença, clientes,
#    medições, usuários), e as fotos das paradas (ImagesStopReason). Sem isso,
#    republicar pedia a licença de novo.
Write-Host '== 3/4 Publish do Desktop' -ForegroundColor Cyan
$envAnterior = $null
if (Test-Path (Join-Path $destino '.env')) { $envAnterior = Get-Content (Join-Path $destino '.env') -Raw }
$guardados = Join-Path $backend 'publish-desktop.dados-anteriores'
Remove-Item $guardados -Recurse -Force -ErrorAction SilentlyContinue
New-Item $guardados -ItemType Directory | Out-Null
foreach ($nome in @('smartline.db', 'smartline.db-wal', 'smartline.db-shm', 'ImagesStopReason')) {
    $origem = Join-Path $destino $nome
    if (Test-Path $origem) { Copy-Item $origem (Join-Path $guardados $nome) -Recurse -Force }
}
Remove-Item $destino -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish (Join-Path $backend 'SmartLine.Desktop\SmartLine.Desktop.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $destino
if ($LASTEXITCODE -ne 0) { throw 'publish do Desktop falhou' }

# 4. Monta a pasta: API + wwwroot + .env + servidor.txt vazio (modo servidor)
Write-Host '== 4/4 Montando a pasta' -ForegroundColor Cyan
Copy-Item (Join-Path $publishApi 'SmartLine.API.exe') $destino
Copy-Item $wwwroot (Join-Path $destino 'wwwroot') -Recurse
Get-ChildItem $destino -Filter '*.pdb' | Remove-Item -Force
Get-ChildItem $destino -Filter 'Microsoft.Web.WebView2.*.xml' | Remove-Item -Force
Set-Content (Join-Path $destino 'servidor.txt') '' -NoNewline

# Devolve os dados do pacote anterior (banco e fotos das paradas)
if (Test-Path $guardados) {
    Get-ChildItem $guardados -Force | ForEach-Object { Copy-Item $_.FullName (Join-Path $destino $_.Name) -Recurse -Force }
    Remove-Item $guardados -Recurse -Force
    if (Test-Path (Join-Path $destino 'smartline.db')) { Write-Host 'Banco do pacote anterior preservado (licença, clientes, medições).' -ForegroundColor Green }
}

# .env de produção: o do pacote anterior, senão o de desenvolvimento sem as linhas só de dev
if (-not $envAnterior) {
    $envDev = Join-Path $backend 'SmartLine.Desktop\bin\Debug\net10.0-windows\.env'
    if (Test-Path $envDev) {
        $envAnterior = (Get-Content $envDev | Where-Object { $_ -notmatch '^(RELATORIO_URL_BASE|NAVEGADOR_PDF)=' -and $_ -notmatch '^MQTT_LOG_BRUTO=true' }) -join "`r`n"
    }
}
if ($envAnterior) { Set-Content (Join-Path $destino '.env') $envAnterior -NoNewline }
else { Write-Warning 'Sem .env: crie backend\publish-desktop\.env com JWT_SECRET=... antes de distribuir' }

if ($Zip) {
    # O zip é o pacote para o cliente: vai SEM o banco e sem as fotos desta instalação (o cliente
    # ativa a licença e cadastra o dele; o .env vai, mas a senha do WISE é ajustada lá).
    $arquivoZip = Join-Path $raiz 'SmartLine-Pacote-SelfContained.zip'
    Remove-Item $arquivoZip -Force -ErrorAction SilentlyContinue
    $itens = Get-ChildItem $destino -Force | Where-Object { $_.Name -notlike 'smartline.db*' -and $_.Name -ne 'ImagesStopReason' }
    Compress-Archive -Path $itens.FullName -DestinationPath $arquivoZip
    Write-Host "Zip: $arquivoZip (sem banco e sem fotos)" -ForegroundColor Green
}
Write-Host "Pacote pronto em $destino" -ForegroundColor Green
Write-Host 'Atalho de distribuição: SmartLine.Desktop.exe (precisa do WebView2 Runtime no PC).'
