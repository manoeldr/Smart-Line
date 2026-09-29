// Textos das entradas do WISE por máquina do catálogo.
import { api } from './api'

export interface EntradaWiseDto {
  canal: string
  entrada: number
  tipo: 'Contador' | 'Estado'
  funcao: string
  nome: string
  textoAtivo: string | null
  textoNormal: string | null
  personalizado: boolean
}

export interface SalvarEntradaWiseRequest {
  canal: string
  nome: string
  textoAtivo: string | null
  textoNormal: string | null
}

export const entradasWiseService = {
  obter: (maquinaId: string) => api.get<EntradaWiseDto[]>(`/maquinas/${maquinaId}/entradas-wise`),
  salvar: (maquinaId: string, entradas: SalvarEntradaWiseRequest[]) =>
    api.put<EntradaWiseDto[]>(`/maquinas/${maquinaId}/entradas-wise`, entradas),
  restaurarPadrao: (maquinaId: string) =>
    api.post<EntradaWiseDto[]>(`/maquinas/${maquinaId}/entradas-wise/restaurar-padrao`, {}),
}
