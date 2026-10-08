import { api } from './api'
import type { Linha } from '../types'

export const linhaService = {
  // data ("yyyy-mm-dd"): a sessão daquele dia em cada máquina, no lugar da situação de agora
  getLinhasByCliente: (clienteId: string, data?: string | null) =>
    api.get<Linha[]>(`/clientes/${clienteId}/linhas${data ? `?data=${data}` : ''}`),
}
