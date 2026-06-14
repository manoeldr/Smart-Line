import { api } from './api'

export interface ParadaDto {
  id: string
  sessaoId: string
  motivoId: string | null
  inicio: string
  fim: string | null
}

export const paradaService = {
  abrir: (sessaoId: string, inicio: Date) =>
    api.post<ParadaDto>('/paradas', { sessaoId, inicio: inicio.toISOString() }),

  fechar: (paradaId: string, motivoId: string, fim: Date) =>
    api.patch<ParadaDto>(`/paradas/${paradaId}/fechar`, { motivoId, fim: fim.toISOString() }),
}
