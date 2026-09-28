using SmartLine.Core.Enums;
using SmartLine.Core.Iot;

namespace SmartLine.Core.Interfaces;

/// <summary>
/// Edição das regras que dão o motivo das paradas no Semi Automático.
/// Dois níveis: o padrão do catálogo (vale para toda máquina daquele tipo) e
/// o personalizado de uma máquina da linha (substitui o do catálogo nela).
/// </summary>
/// <remarks>
/// Uma coleta usa as regras que valiam quando ela começou (ou quando o backend
/// reiniciou e a retomou). Mudanças valem para as próximas.
/// </remarks>
public interface IRegrasClassificacaoService
{
    /// <summary>Regras do catálogo para a máquina (cria o padrão se ainda não tiver). Não encontrado: máquina não existe.</summary>
    Task<ResultadoCadastro<ConjuntoRegrasDto>> DoCatalogoAsync(Guid maquinaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Substitui as regras do catálogo pela lista enviada. A ordem da lista é a
    /// prioridade (a primeira é avaliada primeiro). Regra com Id existente é
    /// atualizada; sem Id (ou Id de outro conjunto) é criada; a que não vier é apagada.
    /// </summary>
    Task<ResultadoCadastro<ConjuntoRegrasDto>> SalvarDoCatalogoAsync(Guid maquinaId, SalvarRegrasRequest request, CancellationToken cancellationToken = default);

    /// <summary>Volta as regras do catálogo às quatro padrão. Os motivos existentes são reaproveitados.</summary>
    Task<ResultadoCadastro<ConjuntoRegrasDto>> RestaurarPadraoAsync(Guid maquinaId, CancellationToken cancellationToken = default);

    /// <summary>Regras que valem para a máquina da linha: as personalizadas, se houver; senão, as do catálogo.</summary>
    Task<ResultadoCadastro<ConjuntoRegrasDto>> DaMaquinaLinhaAsync(Guid maquinaLinhaId, CancellationToken cancellationToken = default);

    /// <summary>Personaliza as regras da máquina da linha (cria o conjunto na primeira vez). Mesmas regras de <see cref="SalvarDoCatalogoAsync"/>.</summary>
    Task<ResultadoCadastro<ConjuntoRegrasDto>> SalvarDaMaquinaLinhaAsync(Guid maquinaLinhaId, SalvarRegrasRequest request, CancellationToken cancellationToken = default);

    /// <summary>Apaga a personalização: a máquina da linha volta a usar as regras do catálogo (devolvidas).</summary>
    Task<ResultadoCadastro<ConjuntoRegrasDto>> RemoverPersonalizacaoAsync(Guid maquinaLinhaId, CancellationToken cancellationToken = default);
}

/// <param name="ConjuntoId">Nulo só se a máquina ainda não tem conjunto (não deveria acontecer).</param>
/// <param name="MaquinaLinhaId">Preenchido quando o conjunto é o personalizado da máquina da linha.</param>
/// <param name="Personalizado">True: regras próprias da máquina da linha. False: as do catálogo.</param>
public record ConjuntoRegrasDto(
    Guid? ConjuntoId,
    Guid MaquinaId,
    string Maquina,
    Guid? MaquinaLinhaId,
    bool Personalizado,
    IReadOnlyList<RegraDto> Regras);

/// <param name="Prioridade">1 = avaliada primeiro. A primeira regra com todas as condições verdadeiras dá o motivo.</param>
public record RegraDto(
    Guid Id,
    int Prioridade,
    string Nome,
    Guid MotivoParadaId,
    string Motivo,
    TipoParada Tipo,
    bool MotivoAtivo,
    bool Ativa,
    IReadOnlyList<CondicaoDto> Condicoes);

/// <param name="Canal">Sensor de estado (S1, S4, S7, S8) nas condições de sensor; nulo na de tempo.</param>
/// <param name="TempoMinimoSegundos">Só na condição de tempo: parada há pelo menos este tempo.</param>
public record CondicaoDto(TipoCondicao Tipo, CanalWise? Canal = null, int? TempoMinimoSegundos = null);

/// <param name="Regras">Na ordem de prioridade.</param>
public record SalvarRegrasRequest(IList<SalvarRegraRequest> Regras);

/// <param name="Id">Da regra existente a atualizar; nulo para criar.</param>
/// <param name="Nome">Vazio = nome do motivo.</param>
public record SalvarRegraRequest(Guid? Id, string? Nome, Guid MotivoParadaId, bool Ativa, IList<CondicaoDto> Condicoes);
