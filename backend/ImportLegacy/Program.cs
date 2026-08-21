using Microsoft.Data.Sqlite;

var origem = @"D:\Smart Line\backend\SmartLine.API\bin\Debug\net10.0\smartline.db";
var destino = @"D:\Smart Line\backend\SmartLine.Desktop\bin\Debug\net10.0-windows\smartline.db";

// Apaga o banco antigo (e seus arquivos auxiliares) antes de gerar a cópia nova
File.Delete(destino);
if (File.Exists(destino + "-wal")) File.Delete(destino + "-wal");
if (File.Exists(destino + "-shm")) File.Delete(destino + "-shm");

using var connection = new SqliteConnection($"Data Source={origem}");
connection.Open();

// VACUUM INTO gera uma cópia limpa e consistente do banco
using var cmd = connection.CreateCommand();
cmd.CommandText = $"VACUUM INTO '{destino.Replace("\\", "/")}'";
cmd.ExecuteNonQuery();

Console.WriteLine("Cópia limpa gerada com sucesso!");