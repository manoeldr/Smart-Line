using Microsoft.EntityFrameworkCore;
using SmartLine.Infrastructure.Data;

var caminhoDb = @"D:\Smart Line\backend\SmartLine.API\bin\Debug\net10.0\smartline.db";

var optionsBuilder = new DbContextOptionsBuilder<SmartLineDbContext>();
optionsBuilder.UseSqlite($"Data Source={caminhoDb}");
using var context = new SmartLineDbContext(optionsBuilder.Options);

var sessoes = context.Sessoes
    .Include(s => s.MaquinaLinha).ThenInclude(ml => ml.Maquina)
    .OrderByDescending(s => s.Inicio)
    .Take(10)
    .ToList();

Console.WriteLine("Últimas 10 sessões (medições) registradas:");
Console.WriteLine();
foreach (var s in sessoes)
{
    Console.WriteLine($"Máquina: {s.MaquinaLinha.Maquina.Nome}");
    Console.WriteLine($"  Início: {s.Inicio}");
    Console.WriteLine($"  Fim: {s.Fim}");
    Console.WriteLine($"  Status: {s.Status}");
    Console.WriteLine();
}