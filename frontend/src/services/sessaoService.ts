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

// Uma linha de leitura já registrada no banco — usada pra reconstruir a tela de Medição
// inteiramente a partir do servidor (não do localStorage), ao retomar uma sessão ativa.
export interface LeituraReconstruidaDto {
  hora: string
  inicial: boolean
  producao: number | null
  extras: Record<string, number>
}

export interface SessaoAtivaDto {
  sessaoId: string
  maquinaLinhaId: string
  maquinaId: string
  maquinaNome: string
  critica: boolean
  medeProducao: boolean
  velocidadeNominal: number
  sobreVelocidade: number
  linhaId: string
  linhaNome: string
  inicio: string
  previsaoTermino: string | null
  camposSelecionados: string[]
  status: 'Rodando' | 'Parada' | 'Pausada'
  leituras: LeituraReconstruidaDto[]
  paradaAtivaId: string | null
  paradaAtivaInicio: string | null
  segundosTotalParadoMs: number
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

  // Retorna null (não erro) se o usuário não tiver sessão ativa — trata o 404 como resultado normal
  getSessaoAtivaDoUsuario: async (): Promise<SessaoAtivaDto | null> => {
    try {
      return await api.get<SessaoAtivaDto>('/sessoes/ativa-do-usuario')
    } catch (e: unknown) {
      const mensagem = e instanceof Error ? e.message : ''
      if (mensagem.includes('404')) return null
      throw e
    }
  },
}