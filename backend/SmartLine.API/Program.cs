using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using SmartLine.Infrastructure.Data;

Env.Load(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env"));

var builder = WebApplication.CreateBuilder(args);

// Banco de dados
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
                       ?? throw new InvalidOperationException("DATABASE_URL não configurada.");

builder.Services.AddDbContext<SmartLineDbContext>(options =>
    options.UseNpgsql(connectionString));

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();