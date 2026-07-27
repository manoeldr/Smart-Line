import { api } from './api'

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
}

export const dashboardService = {
  getDashboardLinha: (linhaId: string, inicio: string, fim: string) =>
    api.get<MaquinaDashboardDto[]>(`/dashboard/linhas/${linhaId}?inicio=${inicio}&fim=${fim}`),
}