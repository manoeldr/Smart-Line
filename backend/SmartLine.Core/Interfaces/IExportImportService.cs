namespace SmartLine.Core.Interfaces;

public interface IExportImportService
{
    Task<byte[]> ExportarAsync(ExportOpcoes opcoes);
    Task<ImportResumoDto> ImportarAsync(byte[] arquivoZip);
    Task<ImportResumoDto> PreviaImportacaoAsync(byte[] arquivoZip);
}

// ── Opções de exportação (categorias marcadas pelo usuário) ──────
public record ExportOpcoes(
    bool ClientesLinhas,
    bool Maquinas,
    bool SessoesMedicoes,
    bool Usuarios
);

// ── Estrutura do dados.json dentro do pacote .zip ─────────────────
public record ExportPacoteDto(
    DateTime GeradoEm,
    IList<ClienteExportDto> Clientes,
    IList<MaquinaExportDto> Maquinas,
    IList<SessaoExportDto> Sessoes,
    IList<UsuarioExportDto> Usuarios
);

public record ClienteExportDto(
    Guid Id,
    string Nome,
    string? Estado,
    bool Ativo,
    IList<LinhaExportDto> Linhas
);

public record LinhaExportDto(
    Guid Id,
    string Nome,
    bool Ativo,
    IList<MaquinaLinhaExportDto> Maquinas
);

public record MaquinaLinhaExportDto(
    Guid Id,
    Guid MaquinaId,
    string TipoColeta,
    decimal VelocidadeNominal,
    decimal SobreVelocidade,
    bool Critica,
    int Ordem,
    bool Ativo
);

public record MaquinaExportDto(
    Guid Id,
    string Nome,
    string? Descricao,
    bool Ativo,
    IList<CampoMaquinaExportDto> Campos,
    IList<MotivoParadaExportDto> Motivos
);

public record CampoMaquinaExportDto(
    Guid Id,
    string Nome,
    string? Unidade,
    int Ordem,
    bool Ativo
);

public record MotivoParadaExportDto(
    Guid Id,
    string Nome,
    string Tipo,
    bool Ativo
);

public record SessaoExportDto(
    Guid Id,
    Guid MaquinaLinhaId,
    Guid UsuarioId,
    DateTime Inicio,
    DateTime? Fim,
    DateTime? PrevisaoTermino,
    string Status,
    string TipoColeta,
    decimal VelocidadeNominal,
    decimal SobreVelocidade,
    IList<ProducaoExportDto> Producoes,
    IList<ParadaExportDto> Paradas,
    IList<LeituraExtraExportDto> LeiturasExtra,
    IList<Guid> CamposSelecionados
);

public record ProducaoExportDto(Guid Id, int Quantidade, int Refugo, DateTime Hora);

public record ParadaExportDto(
    Guid Id,
    Guid? MotivoId,
    DateTime Inicio,
    DateTime? Fim,
    string? FotoPath
);

public record LeituraExtraExportDto(Guid Id, Guid CampoMaquinaId, decimal Valor, DateTime Hora);

public record UsuarioExportDto(
    Guid Id,
    Guid? ClienteId,
    string Nome,
    string Login,
    string SenhaHash,
    string Nivel,
    bool Ativo
);

// ── Resultado de importação (contagem por categoria) ──────────────
public record ImportResumoDto(
    int Clientes,
    int Linhas,
    int Maquinas,
    int Sessoes,
    int Usuarios
);