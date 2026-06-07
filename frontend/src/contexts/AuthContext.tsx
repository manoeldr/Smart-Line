import { createContext, useContext, useState, useEffect, type ReactNode } from 'react'
import type { Usuario } from '../types'

interface AuthState {
  usuario: Usuario | null
  token: string | null
  clienteId: string | null
}

interface AuthContextType extends AuthState {
  login: (token: string, usuario: Usuario) => void
  logout: () => void
  setClienteId: (id: string) => void
  isAuthenticated: boolean
}

const AuthContext = createContext<AuthContextType | null>(null)

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth deve ser usado dentro de AuthProvider')
  return ctx
}

function parseToken(token: string): Usuario | null {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]))
    return {
      id: payload.sub,
      clienteId: payload.clienteId || null,
      nome: payload.nome,
      login: '',
      nivel: payload.nivel,
      ativo: true,
    }
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>(() => {
    const token = localStorage.getItem('token')
    const clienteId = localStorage.getItem('clienteId')
    const usuario = token ? parseToken(token) : null
    if (token && !usuario) localStorage.removeItem('token')
    return { token: usuario ? token : null, usuario, clienteId }
  })

  useEffect(() => {
    if (!state.token) {
      localStorage.removeItem('token')
      localStorage.removeItem('clienteId')
    }
  }, [state.token])

  function login(token: string, usuario: Usuario) {
    localStorage.setItem('token', token)
    const clienteId = usuario.clienteId ?? null
    if (clienteId) localStorage.setItem('clienteId', clienteId)
    setState({ token, usuario, clienteId })
  }

  function logout() {
    localStorage.removeItem('token')
    localStorage.removeItem('clienteId')
    setState({ token: null, usuario: null, clienteId: null })
  }

  function setClienteId(id: string) {
    localStorage.setItem('clienteId', id)
    setState(s => ({ ...s, clienteId: id }))
  }

  return (
    <AuthContext.Provider value={{ ...state, login, logout, setClienteId, isAuthenticated: !!state.token }}>
      {children}
    </AuthContext.Provider>
  )
}