using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartLine.API.Autorizacao;
using SmartLine.API.Middleware;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Services;
using SmartLine.Infrastructure.Data;
using SmartLine.Infrastructure.Repositories;
using SmartLine.Iot.Broker;
using SmartLine.Iot.Coleta;
using System.Text;

// Procura o .env primeiro ao lado do executável (cenário empacotado/produção),
// e só então tenta o caminho relativo de desenvolvimento (dois níveis acima, `dotnet run`).
// Se nenhum existir, segue sem erro — as variáveis podem vir de outro lugar (ex: variáveis de ambiente do SO).
var envAoLadoDoExe = Path.Combine(AppContext.BaseDirectory, ".env");
var envDesenvolvimento = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env");

if (File.Exists(envAoLadoDoExe))
    Env.Load(envAoLadoDoExe);
else if (File.Exists(envDesenvolvimento))
    Env.Load(envDesenvolvimento);

var builder = WebApplication.CreateBuilder(args);

// Banco de dados — SQLite, arquivo local ao lado do executável.
// Pode ser sobrescrito via DATABASE_URL (ex: "Data Source=/caminho/customizado.db"),
// mas por padrão usa um arquivo "smartline.db" na pasta da aplicação.
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
                       ?? $"Data Source={Path.Combine(AppContext.BaseDirectory, "smartline.db")}";
builder.Services.AddDbContext<SmartLineDbContext>(options =>
    options.UseSqlite(connectionString));

// Serviços
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<ISessaoService, SessaoService>();
builder.Services.AddScoped<IMaquinaService, MaquinaService>();
builder.Services.AddScoped<IParadaRegistroService, ParadaRegistroService>();
builder.Services.AddScoped<IProducaoService, ProducaoService>();
builder.Services.AddScoped<ILeituraExtraService, LeituraExtraService>();
builder.Services.AddScoped<ILinhaMaquinaService, LinhaMaquinaService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ISessaoDetalheService, SessaoDetalheService>();
builder.Services.AddScoped<IExportImportService, ExportImportService>();
builder.Services.AddSingleton<IModeloRelatorioService, ModeloRelatorioService>();
builder.Services.AddScoped<ILicencaService, LicencaService>();
builder.Services.AddScoped<IOeeService, OeeService>();
builder.Services.AddScoped<IParadaService, ParadaService>();
builder.Services.AddScoped<IConfiguracaoService, ConfiguracaoService>();
builder.Services.AddScoped<IRegrasPadraoService, RegrasPadraoService>();
builder.Services.AddScoped<IAcompanhamentoService, AcompanhamentoService>();
builder.Services.AddScoped<IRegistradorColeta, SmartLine.Infrastructure.Coleta.RegistradorColeta>();
builder.Services.AddScoped<IRetomadaColetaService, SmartLine.Infrastructure.Coleta.RetomadaColetaService>();
builder.Services.AddScoped<ILocalizadorColetaIot, SmartLine.Infrastructure.Coleta.LocalizadorColetaIot>();
builder.Services.AddScoped<IClassificacaoParadaService, ClassificacaoParadaService>();
builder.Services.AddScoped<IRegrasClassificacaoService, RegrasClassificacaoService>();
builder.Services.AddScoped<IEntradasWiseService, EntradasWiseService>();
builder.Services.AddScoped<IWiseService, WiseService>();

// Coleta automática: relógio injetável (testes usam FakeTimeProvider) e parâmetros globais.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new SmartLine.Core.Coleta.OpcoesColetaIot());

// Semi Automático: broker MQTT embutido — o WISE publica direto neste PC (porta 1883).
// Configurável por MQTT_HABILITADO, MQTT_PORTA e MQTT_LOG_BRUTO (ver OpcoesBrokerMqtt).
// Singleton registrado também como hosted service, para outras partes poderem consultar
// se ele está de pé (tela de dispositivos, passo 5).
builder.Services.AddSingleton(OpcoesBrokerMqtt.DoAmbiente());
builder.Services.AddSingleton<CaixaDeEntradaMqtt>();
builder.Services.AddSingleton<BrokerMqttService>();
builder.Services.AddSingleton<SmartLine.Iot.Rede.ITestePing, SmartLine.Iot.Rede.TestePing>();
// Zera os contadores do WISE (pela interface HTTP dele) ao iniciar cada medição.
builder.Services.AddSingleton(SmartLine.Iot.Rede.OpcoesHttpWise.LerDoAmbiente());
builder.Services.AddSingleton<SmartLine.Iot.Rede.ZeradorContadoresWise>();
// WISE simulado dentro do backend é zerado direto, sem HTTP; o real vai pela rede.
builder.Services.AddSingleton<SmartLine.Iot.Rede.IZeradorContadoresWise>(sp => new SmartLine.Iot.Rede.ZeradorComSimulador(
    sp.GetRequiredService<SmartLine.Iot.Simulacao.SimuladorWise>(),
    sp.GetRequiredService<SmartLine.Iot.Rede.ZeradorContadoresWise>()));

// Área do Desenvolvedor: as últimas linhas do log ficam em memória para a tela, e o
// simulador de WISE pode ser ligado por dentro do sistema (publica no broker acima).
var logEmMemoria = new SmartLine.Iot.Diagnostico.LogEmMemoria();
builder.Logging.AddProvider(logEmMemoria);
builder.Services.AddSingleton(logEmMemoria);
builder.Services.AddSingleton(sp =>
{
    var simulador = new SmartLine.Iot.Simulacao.SimuladorWise();
    var log = sp.GetRequiredService<ILogger<SmartLine.Iot.Simulacao.SimuladorWise>>();
    simulador.Mensagem += texto => log.LogInformation("{Texto}", texto);
    simulador.Publicacao += texto => log.LogDebug("{Texto}", texto);
    return simulador;
});
builder.Services.AddHostedService(sp => sp.GetRequiredService<BrokerMqttService>());

// Motor da coleta: consome o que o broker recebeu e grava paradas, comunicação e produção.
// Registrado depois do broker (sobe depois dele). Singleton para a tela ao vivo (passo 5)
// consultar o estado de cada máquina.
builder.Services.AddSingleton<ColetaIotService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ColetaIotService>());

// JWT
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
                ?? throw new InvalidOperationException("JWT_SECRET não configurada.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
        };
    });
builder.Services.AddAuthorization(Politicas.Registrar);

// Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()
        );
    });

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Aplica migrations pendentes automaticamente ao iniciar — essencial para instalações novas
// (o cliente nunca vai rodar `dotnet ef` manualmente; o app precisa se preparar sozinho).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SmartLineDbContext>();
    db.Database.Migrate();

    // Regras padrão do Semi Automático para toda máquina do catálogo que ainda não tiver
    // (instalação nova, máquinas vindas de importação). Idempotente: não mexe no que existe.
    var regrasPadrao = scope.ServiceProvider.GetRequiredService<IRegrasPadraoService>();
    await regrasPadrao.GarantirParaTodasAsync();

    // WISE simulados (IP 127.0.0.x) só existem enquanto o simulador da Área do Desenvolvedor
    // está ligado; um que tenha sobrado no cadastro (backend derrubado com ele ligado) sai aqui.
    // Um WISE real nunca tem endereço de loopback.
    // Sempre existe um usuário "desenvolvedor". A senha dele não fica no banco: é derivada do
    // segredo da licença e do MAC deste computador (ILicencaService.GerarSenhaDesenvolvedor), então
    // nem Administrador nem quem edita o banco consegue trocá-la.
    if (!await db.Usuarios.AnyAsync(u => u.Nivel == SmartLine.Core.Enums.NivelUsuario.Desenvolvedor))
    {
        db.Usuarios.Add(new SmartLine.Core.Entities.Tenant.Usuario
        {
            Id = Guid.NewGuid(),
            Nome = "Desenvolvedor",
            Login = "desenvolvedor",
            SenhaHash = "(senha derivada do segredo; não fica no banco)",
            Nivel = SmartLine.Core.Enums.NivelUsuario.Desenvolvedor,
            Ativo = true,
        });
        await db.SaveChangesAsync();
    }
    if (app.Environment.IsDevelopment())
    {
        // Em desenvolvimento, a senha do Desenvolvedor desta máquina vai para o log (console e aba Logs).
        var licenca = scope.ServiceProvider.GetRequiredService<ILicencaService>();
        app.Logger.LogInformation("Senha do Desenvolvedor neste computador (MAC {Mac}): {Senha}",
            licenca.ObterMacAddress(), licenca.GerarSenhaDesenvolvedor(licenca.ObterMacAddress()));
    }

    var simuladosSobrando = await db.Wises.Where(w => w.EnderecoIp.StartsWith("127.")).ToListAsync();
    if (simuladosSobrando.Count > 0)
    {
        db.Wises.RemoveRange(simuladosSobrando);
        await db.SaveChangesAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Permite conexões de outros dispositivos na mesma rede (não só localhost) — necessário
// pra outros PCs/tablets acessarem esse backend como "servidor central". Em modo local
// (um PC sozinho), isso não muda nada visível, só amplia quem PODE se conectar.
app.Urls.Clear();
app.Urls.Add("http://0.0.0.0:5278");

app.UseMiddleware<LicencaMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Serve o frontend buildado (React) a partir de wwwroot, quando presente.
// Em desenvolvimento, o frontend roda separado via `npm run dev` (porta 5173) e não usa isso.
// Na versão empacotada (.exe), o frontend já vem buildado dentro de wwwroot, servido pelo mesmo processo.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();