import { api } from './api'
import type { Linha } from '../types'

export const linhaService = {
  getLinhasByCliente: (clienteId: string) =>
    api.get<Linha[]>(`/clientes/${clienteId}/linhas`),
}
