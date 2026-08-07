using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartLine.API.Middleware;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Services;
using SmartLine.Infrastructure.Data;
using SmartLine.Infrastructure.Repositories;
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
builder.Services.AddScoped<ILicencaService, LicencaService>();
builder.Services.AddScoped<IOeeService, OeeService>();
builder.Services.AddScoped<IParadaService, ParadaService>();
builder.Services.AddScoped<IConfiguracaoService, ConfiguracaoService>();

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
builder.Services.AddAuthorization();

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