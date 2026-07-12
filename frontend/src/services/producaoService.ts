import { api } from './api'

export interface ProducaoDto {
  id: string
  sessaoId: string
  quantidade: number
  refugo: number
  hora: string
}

export const producaoService = {
  registrar: (sessaoId: string, quantidade: number, refugo: number, hora: Date) =>
    api.post<ProducaoDto>('/producoes', {
      sessaoId,
      quantidade,
      refugo,
      hora: hora.toISOString(),
    }),
}
