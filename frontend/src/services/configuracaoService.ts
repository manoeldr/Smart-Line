import { api } from './api'

export interface UsuarioConfDto {
  id: string
  nome: string
  login: string
  nivel: string
  ativo: boolean
  clienteId: string | null
  clienteNome: string | null
}

export interface ClienteConfDto {
  id: string
  nome: string
  estado: string | null
  ativo: boolean
}

export interface LinhaConfDto {
  id: string
  nome: string
  clienteId: string
  clienteNome: string
  ativo: boolean
}

export interface MaquinaConfDto {
  id: string
  nome: string
  fabricante: string | null
  descricao: string | null
  ativo: boolean
}

export interface CampoMaquinaDto {
  id: string
  maquinaId: string
  nome: string
  unidade: string | null
  ordem: number
  ativo: boolean
}

export const configuracaoService = {
  // Usuários
  getUsuarios: () => api.get<UsuarioConfDto[]>('/configuracao/usuarios'),
  criarUsuario: (data: object) => api.post<UsuarioConfDto>('/configuracao/usuarios', data),
  editarUsuario: (id: string, data: object) => api.put<UsuarioConfDto>(`/configuracao/usuarios/${id}`, data),
  deletarUsuario: (id: string) => api.delete(`/configuracao/usuarios/${id}`),

  // Clientes
  getClientes: () => api.get<ClienteConfDto[]>('/configuracao/clientes'),
  criarCliente: (data: object) => api.post<ClienteConfDto>('/configuracao/clientes', data),
  editarCliente: (id: string, data: object) => api.put<ClienteConfDto>(`/configuracao/clientes/${id}`, data),
  deletarCliente: (id: string) => api.delete(`/configuracao/clientes/${id}`),

  // Linhas
  getLinhas: () => api.get<LinhaConfDto[]>('/configuracao/linhas'),
  criarLinha: (data: object) => api.post<LinhaConfDto>('/configuracao/linhas', data),
  editarLinha: (id: string, data: object) => api.put<LinhaConfDto>(`/configuracao/linhas/${id}`, data),
  deletarLinha: (id: string) => api.delete(`/configuracao/linhas/${id}`),

  // Máquinas
  getMaquinas: () => api.get<MaquinaConfDto[]>('/configuracao/maquinas'),
  criarMaquina: (data: object) => api.post<MaquinaConfDto>('/configuracao/maquinas', data),
  editarMaquina: (id: string, data: object) => api.put<MaquinaConfDto>(`/configuracao/maquinas/${id}`, data),
  deletarMaquina: (id: string) => api.delete(`/configuracao/maquinas/${id}`),

  // Campos de Máquina
  getCamposMaquina: (maquinaId: string) => api.get<CampoMaquinaDto[]>(`/configuracao/maquinas/${maquinaId}/campos`),
  criarCampoMaquina: (maquinaId: string, data: object) => api.post<CampoMaquinaDto>(`/configuracao/maquinas/${maquinaId}/campos`, data),
  editarCampoMaquina: (id: string, data: object) => api.put<CampoMaquinaDto>(`/configuracao/campos/${id}`, data),
  deletarCampoMaquina: (id: string) => api.delete(`/configuracao/campos/${id}`),
}