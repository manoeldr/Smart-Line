using Microsoft.Data.Sqlite;

var origem = @"D:\Smart Line\backend\SmartLine.API\bin\Debug\net10.0\smartline.db";
var destino = @"D:\Smart Line\backend\SmartLine.Desktop\bin\Debug\net10.0-windows\smartline.db";

using var connection = new SqliteConnection($"Data Source={origem}");
connection.Open();

// VACUUM INTO gera uma cópia limpa e consistente do banco — resolve o problema de arquivos
// -wal/-shm ficarem "soltos" quando copiamos só o arquivo .db bruto (que pode corromper o banco).
using var cmd = connection.CreateCommand();
cmd.CommandText = $"VACUUM INTO '{destino.Replace("\\", "/")}'";
cmd.ExecuteNonQuery();

Console.WriteLine("Cópia limpa gerada com sucesso!");