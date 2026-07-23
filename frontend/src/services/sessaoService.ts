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
  camposSelecionados: string[]
}

export interface AbrirSessaoParams {
  maquinaLinhaId: string
  velocidadeNominal: number
  sobreVelocidade: number
  previsaoTermino: string | null
  tipoColeta: string
  campoMaquinaIds: string[]
}

export interface FinalizarExtra {
  campoMaquinaId: string
  valor: number
}

export const sessaoService = {
  abrir: (params: AbrirSessaoParams) =>
    api.post<SessaoDto>('/sessoes', params),

  fechar: (sessaoId: string) =>
    api.patch<void>(`/sessoes/${sessaoId}/fechar`),

  getById: (sessaoId: string) =>
    api.get<SessaoDto>(`/sessoes/${sessaoId}`),

  estender: (sessaoId: string, previsaoTermino: string) =>
    api.patch<SessaoDto>(`/sessoes/${sessaoId}/estender`, { previsaoTermino }),

  finalizar: (sessaoId: string, producaoFinal: number, refugoFinal: number, extras: FinalizarExtra[]) =>
    api.patch<void>(`/sessoes/${sessaoId}/finalizar`, {
      producaoFinal,
      refugoFinal,
      extras,
    }),
}