import { api } from './api'

export interface MotivoParadaDto {
  id: string
  nome: string
  tipo: string
}

export const maquinaService = {
  getMotivosParada: (maquinaId: string) =>
    api.get<MotivoParadaDto[]>(`/maquinas/${maquinaId}/motivos-parada`),
}
