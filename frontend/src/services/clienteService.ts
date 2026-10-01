import { api } from './api'
import type { Cliente } from '../types'

export const clienteService = {
  getAll: () => api.get<Cliente[]>('/clientes'),

  // Dias ("yyyy-MM-dd") em que alguma máquina do cliente teve sessão — bolinha azul no calendário
  // Com linhaId, só as máquinas daquela linha
  getDatasComSessao: (clienteId: string, linhaId?: string) =>
    api.get<string[]>(`/clientes/${clienteId}/datas-com-sessao${linhaId ? `?linhaId=${linhaId}` : ''}`),
}
