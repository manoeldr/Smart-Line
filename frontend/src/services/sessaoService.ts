import { api } from './api'

export interface SessaoDto {
  id: string
  maquinaLinhaId: string
  usuarioId: string
  inicio: string
  fim: string | null
  status: string
}

export const sessaoService = {
  abrir: (maquinaLinhaId: string) =>
    api.post<SessaoDto>('/sessoes', { maquinaLinhaId }),

  fechar: (sessaoId: string) =>
    api.patch<void>(`/sessoes/${sessaoId}/fechar`),

  getById: (sessaoId: string) =>
    api.get<SessaoDto>(`/sessoes/${sessaoId}`),
}
