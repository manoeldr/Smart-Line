import { useRef } from 'react'
import { useTheme } from '../../contexts/ThemeContext'
import { useAuth } from '../../contexts/AuthContext'

interface TopbarProps {
  titulo: string
  dataFiltro?: string | null
  onOpenFiltro: (ref: React.RefObject<HTMLButtonElement | null>) => void
}

export default function Topbar({ titulo, dataFiltro, onOpenFiltro }: TopbarProps) {
  const { theme, toggleTheme } = useTheme()
  const { usuario } = useAuth()
  const filtroRef = useRef<HTMLButtonElement>(null)

  const nivelLabel: Record<string, string> = {
    SuperAdmin: 'Super Admin',
    Auditor: 'Auditor',
    Visualizador: 'Visualizador',
  }

  return (
    <header className="h-12 bg-white dark:bg-zinc-900 border-b border-zinc-200 dark:border-zinc-800 flex items-center px-4 gap-3 flex-shrink-0">
      <div className="flex items-center gap-2 flex-1">
        <span className="text-sm font-medium text-zinc-900 dark:text-zinc-100">Sanmartin</span>
        <span className="text-zinc-300 dark:text-zinc-600">/</span>
        <span className="text-sm text-zinc-500">{titulo}</span>
      </div>

      <div className="flex items-center gap-2">
        <div className="flex items-center gap-2 text-xs text-zinc-500">
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>
          </svg>
          <span>{usuario?.nome ?? 'Usuário'}</span>
          {usuario?.nivel && (
            <span className="text-[10px] bg-blue-50 dark:bg-blue-950 text-blue-600 dark:text-blue-400 px-1.5 py-0.5 rounded font-medium">
              {nivelLabel[usuario.nivel] ?? usuario.nivel}
            </span>
          )}
        </div>

        <div className="w-px h-5 bg-zinc-200 dark:bg-zinc-700 mx-1" />

        <span className={`text-xs px-2 py-1 rounded-md border ${
          dataFiltro
            ? 'bg-blue-50 dark:bg-blue-950 text-blue-600 dark:text-blue-400 border-blue-200 dark:border-blue-800'
            : 'bg-zinc-50 dark:bg-zinc-800 text-zinc-500 border-zinc-200 dark:border-zinc-700'
        }`}>
          {dataFiltro
            ? `${dataFiltro.split('-').reverse().join('/')} · histórico`
            : new Date().toLocaleDateString('pt-BR') + ' · ' + new Date().toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}
        </span>

        <button
          ref={filtroRef}
          onClick={() => onOpenFiltro(filtroRef)}
          className={`w-8 h-8 rounded-lg border flex items-center justify-center transition-colors ${
            dataFiltro
              ? 'bg-blue-50 dark:bg-blue-950 border-blue-200 dark:border-blue-800 text-blue-600'
              : 'border-zinc-200 dark:border-zinc-700 text-zinc-500 hover:bg-zinc-100 dark:hover:bg-zinc-800'
          }`}
          title="Filtrar por data"
        >
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3"/>
          </svg>
        </button>

        <button
          onClick={toggleTheme}
          className="w-8 h-8 rounded-lg border border-zinc-200 dark:border-zinc-700 flex items-center justify-center text-zinc-500 hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors"
          title="Alternar tema"
        >
          {theme === 'light' ? (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="5"/><line x1="12" y1="1" x2="12" y2="3"/><line x1="12" y1="21" x2="12" y2="23"/><line x1="4.22" y1="4.22" x2="5.64" y2="5.64"/><line x1="18.36" y1="18.36" x2="19.78" y2="19.78"/><line x1="1" y1="12" x2="3" y2="12"/><line x1="21" y1="12" x2="23" y2="12"/><line x1="4.22" y1="19.78" x2="5.64" y2="18.36"/><line x1="18.36" y1="5.64" x2="19.78" y2="4.22"/>
            </svg>
          ) : (
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/>
            </svg>
          )}
        </button>
      </div>
    </header>
  )
}
