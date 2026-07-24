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
  ativo: boolean
}

export const linhaMaquinaService = {
  getMaquinas: (linhaId: string) =>
    api.get<MaquinaLinhaConfDto[]>(`/configuracao/linhas/${linhaId}/maquinas`),

  adicionar: (linhaId: string, maquinaId: string, critica: boolean, velocidadeNominal: number, sobreVelocidade: number) =>
    api.post<MaquinaLinhaConfDto>(`/configuracao/linhas/${linhaId}/maquinas`, {
      maquinaId,
      critica,
      velocidadeNominal,
      sobreVelocidade,
    }),

  atualizar: (linhaId: string, maquinaLinhaId: string, critica: boolean, velocidadeNominal: number, sobreVelocidade: number) =>
    api.put<MaquinaLinhaConfDto>(`/configuracao/linhas/${linhaId}/maquinas/${maquinaLinhaId}`, {
      critica,
      velocidadeNominal,
      sobreVelocidade,
    }),

  remover: (linhaId: string, maquinaLinhaId: string) =>
    api.delete<void>(`/configuracao/linhas/${linhaId}/maquinas/${maquinaLinhaId}`),

  reordenar: (linhaId: string, itens: { maquinaLinhaId: string; ordem: number }[]) =>
    api.patch<void>(`/configuracao/linhas/${linhaId}/maquinas/reordenar`, { itens }),
}