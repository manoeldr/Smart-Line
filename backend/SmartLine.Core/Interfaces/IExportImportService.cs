namespace SmartLine.Core.Interfaces;

public interface IExportImportService
{
    /// <param name="chamadorDesenvolvedor">Só o Desenvolvedor leva ou traz usuários Desenvolvedor; para os demais eles ficam de fora do pacote e da importação.</param>
    Task<byte[]> ExportarAsync(ExportOpcoes opcoes, bool chamadorDesenvolvedor = false);
    Task<ImportResumoDto> ImportarAsync(byte[] arquivoZip, bool chamadorDesenvolvedor = false);
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
// Campos novos entram com valor padrão para um pacote antigo continuar sendo lido.
public record ExportPacoteDto(
    DateTime GeradoEm,
    IList<ClienteExportDto> Clientes,
    IList<MaquinaExportDto> Maquinas,
    IList<SessaoExportDto> Sessoes,
    IList<UsuarioExportDto> Usuarios,
    IList<PeriodoSemComunicacaoExportDto>? PeriodosSemComunicacao = null,
    IList<WiseExportDto>? Wises = null
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

/// <param name="Regras">Regras personalizadas desta máquina da linha (vazio = usa as do catálogo).</param>
public record MaquinaLinhaExportDto(
    Guid Id,
    Guid MaquinaId,
    string TipoColeta,
    decimal VelocidadeNominal,
    decimal SobreVelocidade,
    bool Critica,
    int Ordem,
    bool Ativo,
    bool MedeProducao = true,
    int TempoDeteccaoParadaSegundos = 60,
    IList<RegraExportDto>? Regras = null
);

/// <param name="TextosEntradas">Nomes e textos das entradas S1–S8 do WISE (Semi Automático).</param>
/// <param name="Regras">Regras de classificação do catálogo (Semi Automático).</param>
public record MaquinaExportDto(
    Guid Id,
    string Nome,
    string? Descricao,
    bool Ativo,
    IList<CampoMaquinaExportDto> Campos,
    IList<MotivoParadaExportDto> Motivos,
    IList<TextoEntradaExportDto>? TextosEntradas = null,
    IList<RegraExportDto>? Regras = null
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

public record TextoEntradaExportDto(
    Guid Id,
    string Canal,
    string Nome,
    string? TextoAtivo,
    string? TextoNormal
);

public record RegraExportDto(
    Guid Id,
    int Prioridade,
    string Nome,
    Guid MotivoParadaId,
    bool Ativa,
    IList<CondicaoRegraExportDto> Condicoes
);

public record CondicaoRegraExportDto(
    Guid Id,
    string Tipo,
    string? Canal,
    int? TempoMinimoSegundos
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

public record ProducaoExportDto(Guid Id, int Quantidade, int Refugo, DateTime Hora, bool SemComunicacao = false);

/// <param name="RegraClassificacaoId">Regra que classificou a parada (Semi Automático); só vale se a regra existir no destino.</param>
public record ParadaExportDto(
    Guid Id,
    Guid? MotivoId,
    DateTime Inicio,
    DateTime? Fim,
    string? FotoPath,
    Guid? RegraClassificacaoId = null,
    IList<HistoricoClassificacaoExportDto>? Historico = null
);

public record HistoricoClassificacaoExportDto(
    Guid Id,
    Guid? MotivoAnteriorId,
    Guid? MotivoNovoId,
    Guid? UsuarioId,
    DateTime AlteradoEm
);

public record LeituraExtraExportDto(Guid Id, Guid CampoMaquinaId, decimal Valor, DateTime Hora);

/// <summary>Trechos sem comunicação com o WISE, por máquina da linha (vão junto com as sessões).</summary>
public record PeriodoSemComunicacaoExportDto(
    Guid Id,
    Guid MaquinaLinhaId,
    DateTime Inicio,
    DateTime? Fim,
    bool ProducaoNaoRecuperada
);

/// <summary>Cadastro de WISE (Dispositivos IoT): vai junto com Clientes + Linhas. Cada WISE tem o seu IP fixo.</summary>
public record WiseExportDto(Guid Id, string EnderecoIp, string? Nome, DateTime CriadoEm);

public record UsuarioExportDto(
    Guid Id,
    Guid? ClienteId,
    string Nome,
    string Login,
    string SenhaHash,
    string Nivel,
    bool Ativo,
    string? Email = null
);

// ── Resultado de importação (contagem por categoria) ──────────────
/// <param name="Regras">Regras de classificação (do catálogo e personalizadas das máquinas da linha).</param>
/// <param name="TextosEntradas">Textos das entradas do WISE.</param>
/// <param name="Wises">WISE cadastrados.</param>
public record ImportResumoDto(
    int Clientes,
    int Linhas,
    int Maquinas,
    int Sessoes,
    int Usuarios,
    int Regras = 0,
    int TextosEntradas = 0,
    int Wises = 0
);
