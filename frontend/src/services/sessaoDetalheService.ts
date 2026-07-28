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
}

export interface EventoTimelineDto {
  tipo: 'Marcha' | 'Parada'
  horario: string
  motivoNome: string | null
  motivoTipo: string | null
  duracaoMs: number | null
  fotoPath: string | null
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
}

export const sessaoDetalheService = {
  getUltimaSessaoDetalhe: (maquinaLinhaId: string) =>
    api.get<SessaoDetalheDto>(`/maquinas-linha/${maquinaLinhaId}/ultima-sessao-detalhe`),
}