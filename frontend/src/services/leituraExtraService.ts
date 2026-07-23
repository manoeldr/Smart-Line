import { api } from './api'

export interface LeituraExtraDto {
  id: string
  sessaoId: string
  campoMaquinaId: string
  valor: number
  hora: string
}

export const leituraExtraService = {
  registrar: (sessaoId: string, campoMaquinaId: string, valor: number, hora: Date) =>
    api.post<LeituraExtraDto>('/leituras-extra', {
      sessaoId,
      campoMaquinaId,
      valor,
      hora: hora.toISOString(),
    }),
}
