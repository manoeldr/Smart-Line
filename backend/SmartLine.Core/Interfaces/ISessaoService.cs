namespace SmartLine.Core.Interfaces;

public interface ISessaoService
{
    Task<SessaoDto?> AbrirAsync(Guid maquinaLinhaId, Guid usuarioId, AbrirSessaoRequest req);
    Task<bool> FecharAsync(Guid sessaoId);
    Task<SessaoDto?> GetByIdAsync(Guid sessaoId);
    Task<SessaoDto?> EstenderAsync(Guid sessaoId, DateTime novaPrevisaoTermino);
    Task<bool> FinalizarComLeituraAsync(Guid sessaoId, FinalizarSessaoRequest req);
    // Busca a sessão ativa (EmAndamento) do usuário, com tudo já preenchido — permite que a tela
    // de Medição reconstrua o estado inteiro a partir do banco (não só do localStorage do navegador),
    // sobrevivendo a reiniciar o servidor ou trocar de dispositivo.
    Task<SessaoAtivaDto?> GetSessaoAtivaDoUsuarioAsync(Guid usuarioId);
}

public record SessaoDto(
    string Id,
    string MaquinaLinhaId,
    string UsuarioId,
    DateTime Inicio,
    DateTime? Fim,
    DateTime? PrevisaoTermino,
    string Status,
    string TipoColeta,
    decimal VelocidadeNominal,
    decimal SobreVelocidade,
    IList<string> CamposSelecionados
);

public record AbrirSessaoRequest(
    decimal VelocidadeNominal,
    decimal SobreVelocidade,
    DateTime? PrevisaoTermino,
    string TipoColeta,
    IList<Guid> CampoMaquinaIds
);

public record FinalizarSessaoRequest(
    int ProducaoFinal,
    int RefugoFinal,
    IList<LeituraExtraFinalRequest> Extras
);

public record LeituraExtraFinalRequest(Guid CampoMaquinaId, decimal Valor);

// ── Retomar sessão ativa ────────────────────────────────────────────

public record SessaoAtivaDto(
    string SessaoId,
    string MaquinaLinhaId,
    string MaquinaId,
    string MaquinaNome,
    bool Critica,
    bool MedeProducao,
    decimal VelocidadeNominal,
    decimal SobreVelocidade,
    string LinhaId,
    string LinhaNome,
    DateTime Inicio,
    DateTime? PrevisaoTermino,
    IList<string> CamposSelecionados,
    string Status, // "Rodando" | "Parada" | "Pausada"
    IList<LeituraReconstruidaDto> Leituras,
    string? ParadaAtivaId,
    DateTime? ParadaAtivaInicio,
    double SegundosTotalParadoMs
);

public record LeituraReconstruidaDto(
    DateTime Hora,
    bool Inicial,
    int? Producao,
    IDictionary<string, decimal> Extras // campoMaquinaId -> valor
);