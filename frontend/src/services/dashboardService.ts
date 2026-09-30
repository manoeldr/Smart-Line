import { api } from './api'
import type { SituacaoColeta } from './coletaIotService'

export interface MaquinaDashboardDto {
  maquinaLinhaId: string
  maquinaNome: string
  critica: boolean
  oee: number
  disponibilidade: number
  performance: number
  qualidade: number
  producao: number
  refugo: number
  numSessoes: number
  tempoRodandoMs: number
  tempoParadoMs: number
  // Sessão em andamento no período (Manual ou Semi Automático) e a situação dela agora
  aoVivo: boolean
  situacaoAoVivo: SituacaoColeta | null
  acompanhamentoId: string | null
}

export const dashboardService = {
  getDashboardLinha: (linhaId: string, inicio: string, fim: string) =>
    api.get<MaquinaDashboardDto[]>(`/dashboard/linhas/${linhaId}?inicio=${inicio}&fim=${fim}`),
}