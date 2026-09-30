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

export interface PontoProducaoDto {
  hora: string
  quantidade: number
  // Semi Automático: hora ainda em andamento (o valor ainda vai crescer)
  parcial?: boolean
}

export interface EventoTimelineDto {
  tipo: 'Marcha' | 'Parada'
  horario: string
  motivoNome: string | null
  motivoTipo: string | null
  duracaoMs: number | null
  fotoPath: string | null
  // Só nas paradas: para editar o motivo e ver o histórico
  paradaId?: string | null
  motivoId?: string | null
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
}

export const sessaoDetalheService = {
  getUltimaSessaoDetalhe: (maquinaLinhaId: string) =>
    api.get<SessaoDetalheDto>(`/maquinas-linha/${maquinaLinhaId}/ultima-sessao-detalhe`),
}