using Microsoft.EntityFrameworkCore;
using SmartLine.Infrastructure.Data;

var caminhoDbNovo = @"D:\Smart Line\backend\SmartLine.API\bin\Debug\net10.0\smartline.db";

var optionsBuilder = new DbContextOptionsBuilder<SmartLineDbContext>();
optionsBuilder.UseSqlite($"Data Source={caminhoDbNovo}");
using var context = new SmartLineDbContext(optionsBuilder.Options);

var duplicada = context.Paradas.First(p => p.Id == Guid.Parse("c1780341-6a34-4e3b-9690-aac668faaf8a"));
context.Paradas.Remove(duplicada);
context.SaveChanges();

Console.WriteLine("Parada duplicada removida!");