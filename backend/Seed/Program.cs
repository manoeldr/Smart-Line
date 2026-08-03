using Microsoft.Data.Sqlite;

var caminhoDb = @"D:\Smart Line\backend\SmartLine.Desktop\bin\Debug\net10.0-windows\smartline.db";
var connectionString = $"Data Source={caminhoDb}";

using var connection = new SqliteConnection(connectionString);
connection.Open();

void Executar(string sql)
{
    using var cmd = connection.CreateCommand();
    cmd.CommandText = sql;
    cmd.ExecuteNonQuery();
    Console.WriteLine($"OK: {sql.Substring(0, Math.Min(60, sql.Length))}...");
}

var agora = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

// Cliente
Executar($"""
    INSERT INTO Clientes (Id, Nome, Estado, Ativo, CriadoEm)
    VALUES ('ecee6e08-0760-49c0-a391-a650747a4994', 'Sanmartin', 'RS', 1, '{agora}');
    """);

// Máquinas globais
Executar($"""
    INSERT INTO Maquinas (Id, Nome, Descricao, Ativo, CriadoEm)
    VALUES
      ('aaaaaaaa-0000-0000-0000-000000000001', 'Enchedora', NULL, 1, '{agora}'),
      ('aaaaaaaa-0000-0000-0000-000000000002', 'Rotuladora', NULL, 1, '{agora}'),
      ('aaaaaaaa-0000-0000-0000-000000000003', 'Embaladora', NULL, 1, '{agora}'),
      ('aaaaaaaa-0000-0000-0000-000000000004', 'Paletizadora', NULL, 1, '{agora}');
    """);

// Linha
Executar($"""
    INSERT INTO Linhas (Id, ClienteId, Nome, Ativo, CriadoEm)
    VALUES ('bbbbbbbb-0000-0000-0000-000000000001', 'ecee6e08-0760-49c0-a391-a650747a4994', 'Linha 1 - Envase', 1, '{agora}');
    """);

// Máquinas na linha
Executar("""
    INSERT INTO MaquinasLinha (Id, LinhaId, MaquinaId, TipoColeta, VelocidadeNominal, SobreVelocidade, Critica, Ordem, Ativo)
    VALUES
      ('cccccccc-0000-0000-0000-000000000001', 'bbbbbbbb-0000-0000-0000-000000000001', 'aaaaaaaa-0000-0000-0000-000000000001', 0, 1200, 0, 1, 1, 1),
      ('cccccccc-0000-0000-0000-000000000002', 'bbbbbbbb-0000-0000-0000-000000000001', 'aaaaaaaa-0000-0000-0000-000000000002', 0, 1200, 0, 0, 2, 1),
      ('cccccccc-0000-0000-0000-000000000003', 'bbbbbbbb-0000-0000-0000-000000000001', 'aaaaaaaa-0000-0000-0000-000000000003', 0, 1200, 0, 0, 3, 1),
      ('cccccccc-0000-0000-0000-000000000004', 'bbbbbbbb-0000-0000-0000-000000000001', 'aaaaaaaa-0000-0000-0000-000000000004', 0, 1200, 0, 0, 4, 1);
    """);

// Usuário admin (nivel 0 = Administrador), senha "admin123"
Executar($"""
    INSERT INTO Usuarios (Id, ClienteId, Nome, Login, SenhaHash, Nivel, Ativo, CriadoEm)
    VALUES ('ddf247d0-4f66-450e-94f5-fb9139146fdf', NULL, 'Admin', 'admin', '$2a$11$e1yxtC50xqp1zJ/OwqrdT.H8ix1HLfsAEPs/pokIKdz72F63X9M..', 0, 1, '{agora}');
    """);

Console.WriteLine();
Console.WriteLine("Seed concluído com sucesso!");