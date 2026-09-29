// Coleta Semi Automática (WISE): iniciar, finalizar, situação do WISE da máquina.
import { api } from './api'
import type { SituacaoWiseDto } from './dispositivoIotService'

// Canal lido na medição. Multiplicador = garrafas por pulso (1 quando não há multiplicador).
export interface CanalMedicaoRequest {
  canal: string
  multiplicador: number
}

export interface IniciarColetaRequest {
  maquinaLinhaId: string
  velocidadeNominal: number | null
  sobreVelocidade: number | null
  canais: CanalMedicaoRequest[]
}

export interface ColetaIniciadaDto {
  acompanhamentoId: string
  maquinaLinhaId: string
  sessaoId: string
}

export const coletaIotService = {
  iniciar: (dados: IniciarColetaRequest) => api.post<ColetaIniciadaDto>('/coleta-iot/iniciar', dados),
  finalizar: (acompanhamentoId: string) => api.post<void>(`/coleta-iot/${acompanhamentoId}/finalizar`, {}),

  // Situação do WISE da máquina e as 8 entradas ao vivo (funciona sem coleta ligada).
  wiseDaMaquina: (maquinaLinhaId: string) => api.get<SituacaoWiseDto>(`/coleta-iot/maquina/${maquinaLinhaId}/wise`),
}
