import { createContext, useContext, useState, useEffect, type ReactNode } from 'react'
import type { Usuario } from '../types'

// Último cliente escolhido por quem pode trocar de cliente (sobrevive ao logout).
const CHAVE_ULTIMO_CLIENTE = 'smartline.ultimoCliente'
function lerUltimoCliente(): string | null {
  try { return localStorage.getItem(CHAVE_ULTIMO_CLIENTE) } catch { return null }
}

interface AuthState {
  usuario: Usuario | null
  token: string | null
  clienteId: string | null
  // Linha escolhida no topo (Overview mostra só ela; a Medição usa ela). Lembrada por cliente.
  linhaId: string | null
}

interface AuthContextType extends AuthState {
  login: (token: string, usuario: Usuario) => void
  logout: () => void
  setClienteId: (id: string) => void
  setLinhaId: (id: string | null) => void
  isAuthenticated: boolean
}

// Última linha escolhida em cada cliente (sobrevive ao logout, como o último cliente).
function chaveUltimaLinha(clienteId: string) {
  return `smartline.ultimaLinha.${clienteId}`
}
function lerUltimaLinha(clienteId: string | null): string | null {
  if (!clienteId) return null
  try { return localStorage.getItem(chaveUltimaLinha(clienteId)) } catch { return null }
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
    // Vencido: nem entra (vai direto para o login, sem tela cheia de erros).
    if (typeof payload.exp === 'number' && payload.exp * 1000 <= Date.now()) return null
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
    return { token: usuario ? token : null, usuario, clienteId, linhaId: lerUltimaLinha(clienteId) }
  })

  useEffect(() => {
    if (!state.token) {
      localStorage.removeItem('token')
      localStorage.removeItem('clienteId')
    }
  }, [state.token])

  // Qualquer cliente em uso (escolhido agora ou já guardado de antes) vira o "último".
  useEffect(() => {
    if (state.clienteId) {
      try { localStorage.setItem(CHAVE_ULTIMO_CLIENTE, state.clienteId) } catch { /* sem armazenamento */ }
    }
  }, [state.clienteId])

  // O último cliente escolhido fica guardado além do logout (chave própria), para a próxima
  // entrada abrir nele. Quem tem cliente fixo no cadastro usa o dele.
  function login(token: string, usuario: Usuario) {
    localStorage.setItem('token', token)
    const clienteId = usuario.clienteId ?? lerUltimoCliente()
    if (clienteId) localStorage.setItem('clienteId', clienteId)
    setState({ token, usuario, clienteId, linhaId: lerUltimaLinha(clienteId) })
  }

  function logout() {
    localStorage.removeItem('token')
    localStorage.removeItem('clienteId')
    setState({ token: null, usuario: null, clienteId: null, linhaId: null })
  }

  // Trocar de cliente volta para a última linha escolhida nele (ou nenhuma: a tela pega a primeira)
  function setClienteId(id: string) {
    localStorage.setItem('clienteId', id)
    try { localStorage.setItem(CHAVE_ULTIMO_CLIENTE, id) } catch { /* sem armazenamento: só nesta sessão */ }
    setState(s => ({ ...s, clienteId: id, linhaId: lerUltimaLinha(id) }))
  }

  function setLinhaId(id: string | null) {
    setState(s => {
      if (s.clienteId && id) {
        try { localStorage.setItem(chaveUltimaLinha(s.clienteId), id) } catch { /* sem armazenamento */ }
      }
      return { ...s, linhaId: id }
    })
  }

  return (
    <AuthContext.Provider value={{ ...state, login, logout, setClienteId, setLinhaId, isAuthenticated: !!state.token }}>
      {children}
    </AuthContext.Provider>
  )
}