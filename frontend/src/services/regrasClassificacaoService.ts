// Regras que dão o motivo das paradas no Semi Automático (catálogo e personalizadas por máquina da linha).
import { api } from './api'

export type TipoCondicao = 'SensorEmAlarme' | 'SensorForaDeAlarme' | 'TempoParadaMinimo'

export interface CondicaoDto {
  tipo: TipoCondicao
  canal: string | null
  tempoMinimoSegundos: number | null
}

export interface RegraDto {
  id: string
  prioridade: number
  nome: string
  motivoParadaId: string
  motivo: string
  tipo: 'Interna' | 'Externa' | 'Planejada'
  motivoAtivo: boolean
  ativa: boolean
  condicoes: CondicaoDto[]
}

export interface ConjuntoRegrasDto {
  conjuntoId: string | null
  maquinaId: string
  maquina: string
  maquinaLinhaId: string | null
  personalizado: boolean
  regras: RegraDto[]
}

export const regrasClassificacaoService = {
  // Regras que valem para a máquina da linha (personalizadas ou, se não houver, as do catálogo).
  daMaquinaLinha: (maquinaLinhaId: string) =>
    api.get<ConjuntoRegrasDto>(`/regras-classificacao/maquina-linha/${maquinaLinhaId}`),
}
