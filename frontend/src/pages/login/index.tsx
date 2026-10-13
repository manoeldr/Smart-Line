// Tela de login. Não usa o Layout padrão (sem sidebar/topbar), é uma página isolada.
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import { authService } from '../../services/authService'
import { CHAVE_VOLTAR_APOS_LOGIN } from '../../services/api'
import { btnPrimary } from '../../styles/buttons'
import { inputMdFull, label } from '../../styles/inputs'
import Logo from '../../components/Logo'

export default function Login() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({ login: '', senha: '' })
  const [erro, setErro] = useState<string | null>(null)
  // Veio de uma sessão expirada (o login vale 8 h): avisa e, ao entrar, volta para onde estava.
  const [expirou] = useState(() => new URLSearchParams(window.location.search).get('expirou') === '1')
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setErro(null)
    setLoading(true)
    try {
      const res = await authService.login(form)
      localStorage.setItem('token', res.token)
      login(res.token, res.usuario)
      let voltar: string | null = null
      try {
        voltar = sessionStorage.getItem(CHAVE_VOLTAR_APOS_LOGIN)
        sessionStorage.removeItem(CHAVE_VOLTAR_APOS_LOGIN)
      } catch { /* sem armazenamento */ }
      navigate(voltar && voltar.startsWith('/') && !voltar.startsWith('/login') ? voltar : '/overview', { replace: true })
    } catch (err: unknown) {
      setErro(err instanceof Error ? err.message : 'Erro ao fazer login')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-fundo flex items-center justify-center p-4">
      <div className="w-full max-w-sm">
        <div className="flex items-center gap-3 mb-8 justify-center">
          <Logo tamanho={40} />
          <div>
            <p className="text-lg font-medium text-texto">Smart Line Scan</p>
            <p className="text-xs text-texto-2">Sanmartin</p>
          </div>
        </div>
        <div className="bg-superficie border border-borda rounded-xl p-6">
          <h1 className="text-sm font-medium text-texto mb-5">Entrar na sua conta</h1>
          {expirou && (
            <p className="text-xs text-amber-700 dark:text-amber-400 bg-amber-50 dark:bg-amber-950 px-3 py-2 rounded-sm mb-4">
              Sua sessão expirou. Entre de novo para continuar de onde parou.
            </p>
          )}
          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <label className={label}>Login</label>
              <input
                type="text"
                value={form.login}
                onChange={e => setForm(f => ({ ...f, login: e.target.value }))}
                placeholder="seu.login"
                required
                className={`${inputMdFull} rounded-sm`}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className={label}>Senha</label>
              <input
                type="password"
                value={form.senha}
                onChange={e => setForm(f => ({ ...f, senha: e.target.value }))}
                placeholder="••••••••"
                required
                className={`${inputMdFull} rounded-sm`}
              />
            </div>
            {erro && (
              <p className="text-xs text-red-500 bg-red-50 dark:bg-red-950 px-3 py-2 rounded-sm">
                {erro}
              </p>
            )}
            <button type="submit" disabled={loading} className={`${btnPrimary} w-full rounded-sm`}>
              {loading ? 'Entrando...' : 'Entrar'}
            </button>
          </form>
        </div>
      </div>
    </div>
  )
}