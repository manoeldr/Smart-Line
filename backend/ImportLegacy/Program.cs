using Microsoft.EntityFrameworkCore;
using SmartLine.Core.Enums;
using SmartLine.Infrastructure.Data;

var caminhoDbNovo = @"D:\Smart Line\backend\SmartLine.API\bin\Debug\net10.0\smartline.db";

var optionsBuilder = new DbContextOptionsBuilder<SmartLineDbContext>();
optionsBuilder.UseSqlite($"Data Source={caminhoDbNovo}");
using var context = new SmartLineDbContext(optionsBuilder.Options);

// "Falta de Produto" também é uma causa EXTERNA à máquina (falta de produto vindo de outra
// máquina anterior na linha), estava classificada como Interna no backup antigo — corrige aqui.
var motivo = context.MotivosParada.FirstOrDefault(m => m.Nome == "Falta de Produto");

if (motivo is null)
{
    Console.WriteLine("Motivo 'Falta de Produto' não encontrado.");
}
else
{
    Console.WriteLine($"MaquinaId: {motivo.MaquinaId}, Tipo atual: {motivo.Tipo}");
    motivo.Tipo = TipoParada.Externa;
    context.SaveChanges();
    Console.WriteLine("Corrigido para Externa!");
}