// Paradas: pendentes de classificação (coleta automática) e troca do motivo de qualquer
// parada (Manual ou Semi Automático), com histórico de quem trocou.
import { api } from './api'

export interface ParadaColetaDto {
  id: string
  sessaoId: string
  maquinaLinhaId: string
  maquinaId: string
  maquina: string
  linha: string
  cliente: string
  inicio: string
  fim: string | null
  duracaoSegundos: number
  motivoId: string | null
  motivo: string | null
  tipo: 'Interna' | 'Externa' | 'Planejada'
  classificadaPeloSistema: boolean
}

export interface HistoricoClassificacaoDto {
  alteradoEm: string
  motivoAnteriorId: string | null
  motivoAnterior: string | null
  motivoNovoId: string | null
  motivoNovo: string | null
  usuarioId: string | null
  autor: string
}

export interface FiltroPendentes {
  clienteId?: string
  linhaId?: string
  maquinaLinhaId?: string
  desde?: string // ISO
  ate?: string   // ISO
  limite?: number
}

function query(filtro: FiltroPendentes) {
  const params = new URLSearchParams()
  for (const [chave, valor] of Object.entries(filtro)) {
    if (valor !== undefined && valor !== '') params.set(chave, String(valor))
  }
  const texto = params.toString()
  return texto ? `?${texto}` : ''
}

export const paradaColetaService = {
  pendentes: (filtro: FiltroPendentes) =>
    api.get<ParadaColetaDto[]>(`/coleta-iot/paradas/pendentes${query(filtro)}`),

  reclassificar: (paradaId: string, motivoId: string) =>
    api.put<ParadaColetaDto>(`/coleta-iot/paradas/${paradaId}/motivo`, { motivoId }),

  historico: (paradaId: string) =>
    api.get<HistoricoClassificacaoDto[]>(`/coleta-iot/paradas/${paradaId}/historico`),
}
