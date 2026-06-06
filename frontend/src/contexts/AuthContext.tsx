import { createContext, useContext, useState, ReactNode } from 'react'
import type { Usuario } from '../types'

interface AuthState {
  usuario: Usuario | null
  token: string | null
}

interface AuthContextType extends AuthState {
  login: (token: string, usuario: Usuario) => void
  logout: () => void
  isAuthenticated: boolean
}

const AuthContext = createContext<AuthContextType | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({
    usuario: null,
    token: null,
  })

  function login(token: string, usuario: Usuario) {
    setState({ token, usuario })
  }

  function logout() {
    setState({ token: null, usuario: null })
  }

  return (
    <AuthContext.Provider value={{ ...state, login, logout, isAuthenticated: !!state.token }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth deve ser usado dentro de AuthProvider')
  return ctx
}
