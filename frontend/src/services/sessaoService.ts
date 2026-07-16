import { api } from './api'

export interface SessaoDto {
  id: string
  maquinaLinhaId: string
  usuarioId: string
  inicio: string
  fim: string | null
  previsaoTermino: string | null
  status: string
  tipoColeta: string
  velocidadeNominal: number
  sobreVelocidade: number
}

export interface AbrirSessaoParams {
  maquinaLinhaId: string
  velocidadeNominal: number
  sobreVelocidade: number
  previsaoTermino: string | null
  tipoColeta: string
}

export const sessaoService = {
  abrir: (params: AbrirSessaoParams) =>
    api.post<SessaoDto>('/sessoes', params),

  fechar: (sessaoId: string) =>
    api.patch<void>(`/sessoes/${sessaoId}/fechar`),

  getById: (sessaoId: string) =>
    api.get<SessaoDto>(`/sessoes/${sessaoId}`),
}