using SmartLine.Core.Coleta;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;

namespace SmartLine.Iot.Coleta;

/// <summary>
/// Foto do estado de uma máquina em coleta, tirada depois de cada mensagem
/// processada. Imutável: pode ser lida de qualquer thread (tela ao vivo, passo 5).
/// </summary>
/// <param name="ProducaoPendente">Apurada desde a última consolidação, ainda não gravada.</param>
/// <param name="Sensores">Último valor bruto de cada sensor de estado lido.</param>
/// <param name="Contadores">Último valor bruto de cada contador lido.</param>
public sealed record SituacaoColetaIot(
    Guid MaquinaLinhaId,
    Guid AcompanhamentoId,
    SituacaoMaquina Situacao,
    ClassificacaoParada? ClassificacaoAtual,
    DateTime? InicioParadaUtc,
    DateTime? UltimaAmostraUtc,
    ProducaoPendente ProducaoPendente,
    IReadOnlyDictionary<CanalWise, bool> Sensores,
    IReadOnlyDictionary<CanalWise, uint> Contadores);
