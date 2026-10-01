import { api } from './api'
import type { SituacaoColeta } from './coletaIotService'
import type { PontoProducaoDto } from './sessaoDetalheService'

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
  // Sessão mostrada no card: a em andamento, se houver, senão a última do período (nulas sem sessão)
  sessaoInicio: string | null
  sessaoFim: string | null
  tempoRodandoMs: number
  tempoParadoMs: number
  // Sessão em andamento no período (Manual ou Semi Automático) e a situação dela agora
  aoVivo: boolean
  situacaoAoVivo: SituacaoColeta | null
  acompanhamentoId: string | null
}

// Visão geral da linha: OEE pela máquina crítica (a de pior OEE, se houver mais de uma; sem
// crítica com sessão, a pior entre as que medem produção) e as paradas somadas de todas as
// máquinas — sempre a sessão em andamento de cada máquina, senão a última do período.
export interface MaquinaResumoLinhaDto {
  maquinaLinhaId: string
  nome: string
  critica: boolean
  referencia: boolean
  temSessao: boolean
  aoVivo: boolean
  oee: number | null
  producao: number
  refugo: number
  tempoParadoMs: number
  numParadas: number
}

export interface TempoParadoMaquinaDto {
  maquinaLinhaId: string
  duracaoMs: number
}

export interface ParadaLinhaPorMotivoDto {
  motivo: string
  tipo: 'Interna' | 'Externa' | 'Planejada'
  duracaoMs: number
  quantidade: number
  porMaquina: TempoParadoMaquinaDto[]
}

export interface ParadaLinhaPorHoraDto {
  hora: string
  porMaquina: TempoParadoMaquinaDto[]
}

export interface LinhaDashboardDto {
  maquinaReferencia: string | null
  referenciaCritica: boolean
  oee: number | null
  disponibilidade: number
  performance: number | null
  qualidade: number
  producao: number
  refugoTotal: number
  tempoParadoTotalMs: number
  numParadas: number
  maquinas: MaquinaResumoLinhaDto[]
  paradasPorMotivo: ParadaLinhaPorMotivoDto[]
  paradasPorHora: ParadaLinhaPorHoraDto[]
  // Gráfico de produção da máquina de referência (o mesmo do detalhe dela)
  producaoPorHora: PontoProducaoDto[]
}

export const dashboardService = {
  getDashboardLinha: (linhaId: string, inicio: string, fim: string) =>
    api.get<MaquinaDashboardDto[]>(`/dashboard/linhas/${linhaId}?inicio=${inicio}&fim=${fim}`),

  getLinhaGeral: (linhaId: string, inicio: string, fim: string) =>
    api.get<LinhaDashboardDto>(`/dashboard/linhas/${linhaId}/geral?inicio=${inicio}&fim=${fim}`),
}