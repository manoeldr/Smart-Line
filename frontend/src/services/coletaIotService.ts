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

export type SituacaoColeta = 'Rodando' | 'Parada' | 'SemComunicacao' | 'AguardandoPrimeiraAmostra'

export interface ParadaAbertaDto {
  paradaId: string
  inicio: string
  motivoId: string | null
  motivo: string | null
  tipo: 'Interna' | 'Externa' | 'Planejada'
}

export interface ColetaIotResumoDto {
  acompanhamentoId: string
  maquinaLinhaId: string
  maquina: string
  linhaId: string
  linha: string
  cliente: string
  usuarioId: string
  usuario: string
  iniciadoEm: string
  tempoDeteccaoParadaSegundos: number
  canais: CanalMedicaoRequest[]
  enderecoIp: string | null
  sessaoId: string | null
  sessaoInicio: string | null
  velocidadeNominal: number
  producaoConsolidada: number
  refugoConsolidado: number
  ultimaConsolidacao: string | null
  paradaAberta: ParadaAbertaDto | null
  semComunicacaoDesde: string | null
  paradasNaoClassificadas: number
  maquinaId: string
}

export interface SensorAoVivoDto {
  canal: string
  nome: string
  valorBruto: boolean
  emAlarme: boolean
  texto: string
}

// Coleta ligada numa máquina, com o que vem do banco e o estado ao vivo do motor.
export interface ColetaIotPainelDto {
  coleta: ColetaIotResumoDto
  situacao: SituacaoColeta | null
  ultimaMensagem: string | null
  producaoSessao: number
  refugoSessao: number
  sensores: SensorAoVivoDto[]
  contadores: Record<string, number>
  wiseConectado: boolean
}

export const coletaIotService = {
  // Coleta ligada na máquina; a API responde 404 quando a máquina não está em coleta.
  painelDaMaquina: (maquinaLinhaId: string) => api.get<ColetaIotPainelDto>(`/coleta-iot/maquina/${maquinaLinhaId}`),

  iniciar: (dados: IniciarColetaRequest) => api.post<ColetaIniciadaDto>('/coleta-iot/iniciar', dados),
  finalizar: (acompanhamentoId: string) => api.post<void>(`/coleta-iot/${acompanhamentoId}/finalizar`, {}),

  // Situação do WISE da máquina e as 8 entradas ao vivo (funciona sem coleta ligada).
  wiseDaMaquina: (maquinaLinhaId: string) => api.get<SituacaoWiseDto>(`/coleta-iot/maquina/${maquinaLinhaId}/wise`),
}
