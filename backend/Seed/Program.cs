using Microsoft.Data.Sqlite;

// Popula o banco com um usuário administrador padrão. Aponte `caminhoDb` para o smartline.db
// que você quer inicializar (dev, Desktop ou publish).
var caminhoDb = @"D:\Smart Line\backend\SmartLine.API\bin\Debug\net10.0\smartline.db";
var connectionString = $"Data Source={caminhoDb}";

using var connection = new SqliteConnection(connectionString);
connection.Open();

void Executar(string sql)
{
    using var cmd = connection.CreateCommand();
    cmd.CommandText = sql;
    var linhas = cmd.ExecuteNonQuery();
    Console.WriteLine($"OK ({linhas} linha(s)): {sql.Substring(0, Math.Min(60, sql.Length))}...");
}

var agora = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

// Usuário admin (nivel 0 = Administrador), senha "admin123".
// IMPORTANTE: usamos randomblob(16) para gerar o Id, no formato BLOB nativo do SQLite/EF Core —
// nunca insira GUIDs como texto puro à mão, o EF Core não reconhece como o mesmo valor.
Executar($"""
    INSERT INTO Usuarios (Id, ClienteId, Nome, Login, SenhaHash, Nivel, Ativo, CriadoEm)
    VALUES (randomblob(16), NULL, 'Admin', 'admin', '$2a$11$e1yxtC50xqp1zJ/OwqrdT.H8ix1HLfsAEPs/pokIKdz72F63X9M..', 0, 1, '{agora}');
    """);

Console.WriteLine();
Console.WriteLine("Seed concluído — só o usuário admin. Crie Clientes/Linhas/Máquinas pela própria interface.");