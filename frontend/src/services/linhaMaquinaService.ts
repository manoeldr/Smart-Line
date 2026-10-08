import { api } from './api'

export interface MaquinaLinhaConfDto {
  id: string
  linhaId: string
  maquinaId: string
  maquinaNome: string
  ordem: number
  critica: boolean
  velocidadeNominal: number
  sobreVelocidade: number
  medeProducao: boolean
  ativo: boolean
  // Semi Automático
  tempoDeteccaoParadaSegundos?: number
  regrasPersonalizadas?: boolean
}

export const linhaMaquinaService = {
  getMaquinas: (linhaId: string) =>
    api.get<MaquinaLinhaConfDto[]>(`/configuracao/linhas/${linhaId}/maquinas`),

  // tempoDeteccaoParadaSegundos (Z do Semi Automático, 10 a 3600 s): omitido = 60 s
  adicionar: (linhaId: string, maquinaId: string, critica: boolean, velocidadeNominal: number, sobreVelocidade: number, medeProducao: boolean, tempoDeteccaoParadaSegundos?: number) =>
    api.post<MaquinaLinhaConfDto>(`/configuracao/linhas/${linhaId}/maquinas`, {
      maquinaId,
      critica,
      velocidadeNominal,
      sobreVelocidade,
      medeProducao,
      tempoDeteccaoParadaSegundos,
    }),

  // tempoDeteccaoParadaSegundos (Z do Semi Automático): omitido = mantém o atual
  atualizar: (linhaId: string, maquinaLinhaId: string, critica: boolean, velocidadeNominal: number, sobreVelocidade: number, medeProducao: boolean, tempoDeteccaoParadaSegundos?: number) =>
    api.put<MaquinaLinhaConfDto>(`/configuracao/linhas/${linhaId}/maquinas/${maquinaLinhaId}`, {
      critica,
      velocidadeNominal,
      sobreVelocidade,
      medeProducao,
      tempoDeteccaoParadaSegundos,
    }),

  remover: (linhaId: string, maquinaLinhaId: string) =>
    api.delete<void>(`/configuracao/linhas/${linhaId}/maquinas/${maquinaLinhaId}`),

  reordenar: (linhaId: string, itens: { maquinaLinhaId: string; ordem: number }[]) =>
    api.patch<void>(`/configuracao/linhas/${linhaId}/maquinas/reordenar`, { itens }),
}