import { api } from './api'
import type { Cliente } from '../types'

export const clienteService = {
  getAll: () => api.get<Cliente[]>('/clientes'),
}
