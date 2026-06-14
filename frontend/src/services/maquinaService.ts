import { api } from './api'

export interface MotivoParadaDto {
  id: string
  nome: string
  tipo: string
}

export const maquinaService = {
  getMotivosParada: (maquinaId: string) =>
    api.get<MotivoParadaDto[]>(`/maquinas/${maquinaId}/motivos-parada`),

  criarMotivoParada: (maquinaId: string, nome: string, tipo: string) =>
    api.post<MotivoParadaDto>(`/maquinas/${maquinaId}/motivos-parada`, { nome, tipo }),

  criarMotivoParadaPlanejado: (maquinaId: string, nome: string) =>
    api.post<MotivoParadaDto>(`/maquinas/${maquinaId}/motivos-parada`, { nome, tipo: 'Planejada' }),
}