import { api } from './api'

export interface PontoExtraDto {
  hora: string
  valor: number
}

export interface CampoGraficoDto {
  campoMaquinaId: string
  nome: string
  unidade: string | null
  pontos: PontoExtraDto[]
}

// Tempo parado dentro de uma hora (hora = início da hora), por tipo
export interface ParadaPorHoraDto {
  hora: string
  internaMs: number
  externaMs: number
  planejadaMs: number
}

// Tempo parado e número de paradas de um motivo na sessão (maior primeiro)
export interface ParadaPorMotivoDto {
  motivoId: string | null
  motivo: string
  tipo: 'Interna' | 'Externa' | 'Planejada'
  duracaoMs: number
  quantidade: number
}

export interface PontoProducaoDto {
  hora: string
  quantidade: number
  // Semi Automático: hora ainda em andamento (o valor ainda vai crescer)
  parcial?: boolean
  // Semi Automático: produção feita sem comunicação, dividida pelas horas do período (cinza; fora do OEE)
  semComunicacao?: number
}

export interface EventoTimelineDto {
  // SemComunicacao: WISE fora (fica fora do OEE)
  tipo: 'Marcha' | 'Parada' | 'SemComunicacao'
  horario: string
  motivoNome: string | null
  motivoTipo: string | null
  duracaoMs: number | null
  fotoPath: string | null
  // Só nas paradas: para editar o motivo e ver o histórico
  paradaId?: string | null
  motivoId?: string | null
  // Parada ou sem comunicação ainda em curso: duracaoMs é até agora
  emAndamento?: boolean
}

export interface SessaoDetalheDto {
  sessaoId: string
  maquinaNome: string
  inicio: string
  fim: string | null
  status: string
  velocidadeNominal: number
  sobreVelocidade: number
  oee: number
  eficiencia: number
  disponibilidade: number
  qualidade: number
  tempoRodandoMs: number
  tempoParadoMs: number
  producao: number
  refugo: number
  mttrMs: number | null
  mtbfMs: number | null
  camposExtras: CampoGraficoDto[]
  pontosProducao: PontoProducaoDto[]
  eventos: EventoTimelineDto[]
  // Máquina do catálogo (motivos de parada)
  maquinaId?: string | null
  // No Semi Automático o gráfico de produção vem por hora (cada ponto no início da hora)
  tipoColeta?: 'Manual' | 'SemiAutomatico' | 'Automatico' | null
  // Gráficos de paradas (a parada em curso conta até agora)
  paradasPorHora?: ParadaPorHoraDto[] | null
  paradasPorMotivo?: ParadaPorMotivoDto[] | null
}

export const sessaoDetalheService = {
  getUltimaSessaoDetalhe: (maquinaLinhaId: string) =>
    api.get<SessaoDetalheDto>(`/maquinas-linha/${maquinaLinhaId}/ultima-sessao-detalhe`),
}