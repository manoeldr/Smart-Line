import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import { authService } from '../../services/authService'

export default function Login() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({ login: '', senha: '' })
  const [erro, setErro] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setErro(null)
    setLoading(true)
    try {
      const res = await authService.login(form)
      localStorage.setItem('token', res.token)
      login(res.token, res.usuario)
      navigate('/overview')
    } catch (err: unknown) {
      setErro(err instanceof Error ? err.message : 'Erro ao fazer login')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-zinc-100 dark:bg-zinc-950 flex items-center justify-center p-4">
      <div className="w-full max-w-sm">

        <div className="flex items-center gap-3 mb-8 justify-center">
          <div className="w-10 h-10 bg-blue-600 rounded-xl flex items-center justify-center flex-shrink-0">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/>
            </svg>
          </div>
          <div>
            <p className="text-lg font-medium text-zinc-900 dark:text-zinc-100">SmartLine</p>
            <p className="text-xs text-zinc-500">Sanmartin</p>
          </div>
        </div>

        <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 rounded-xl p-6">
          <h1 className="text-sm font-medium text-zinc-900 dark:text-zinc-100 mb-5">Entrar na sua conta</h1>

          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <label className="text-xs text-zinc-500">Login</label>
              <input
                type="text"
                value={form.login}
                onChange={e => setForm(f => ({ ...f, login: e.target.value }))}
                placeholder="seu.login"
                required
                className="h-9 px-3 rounded-sm border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-sm text-zinc-900 dark:text-zinc-100 placeholder:text-zinc-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <label className="text-xs text-zinc-500">Senha</label>
              <input
                type="password"
                value={form.senha}
                onChange={e => setForm(f => ({ ...f, senha: e.target.value }))}
                placeholder="••••••••"
                required
                className="h-9 px-3 rounded-sm border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-sm text-zinc-900 dark:text-zinc-100 placeholder:text-zinc-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              />
            </div>

            {erro && (
              <p className="text-xs text-red-500 bg-red-50 dark:bg-red-950 px-3 py-2 rounded-sm">
                {erro}
              </p>
            )}

            <button
              type="submit"
              disabled={loading}
              className="h-9 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed text-white text-sm font-medium rounded-sm transition-colors"
            >
              {loading ? 'Entrando...' : 'Entrar'}
            </button>
          </form>
        </div>
      </div>
    </div>
  )
}