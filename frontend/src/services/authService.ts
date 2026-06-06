import { api } from './api'
import type { Usuario } from '../types'

interface LoginRequest {
  login: string
  senha: string
}

interface LoginResponse {
  token: string
  usuario: Usuario
}

export const authService = {
  login: (data: LoginRequest) => api.post<LoginResponse>('/auth/login', data),
}
