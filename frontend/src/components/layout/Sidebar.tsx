import { NavLink } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'

export default function Sidebar() {
  const { usuario } = useAuth()

  return (
    <aside className="w-52 bg-white dark:bg-zinc-900 border-r border-zinc-200 dark:border-zinc-800 flex flex-col h-full flex-shrink-0">
      {/* Logo */}
      <div className="px-4 py-5 border-b border-zinc-200 dark:border-zinc-800 flex items-center gap-3">
        <div className="w-8 h-8 bg-blue-600 rounded-lg flex items-center justify-center flex-shrink-0">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="22 12 18 12 15 21 9 3 6 12 2 12" />
          </svg>
        </div>
        <div>
          <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">SmartLine</p>
          <p className="text-xs text-zinc-500">Sanmartin</p>
        </div>
      </div>

      {/* Navegação */}
      <nav className="flex-1 px-2 py-3">
        <p className="px-2 pb-1 text-[10px] font-medium uppercase tracking-widest text-zinc-400">
          Monitoramento
        </p>
        <NavLink
          to="/overview"
          className={({ isActive }) =>
            `flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm transition-colors ${
              isActive
                ? 'bg-blue-50 text-blue-600 dark:bg-blue-950 dark:text-blue-400'
                : 'text-zinc-500 hover:bg-zinc-100 hover:text-zinc-900 dark:hover:bg-zinc-800 dark:hover:text-zinc-100'
            }`
          }
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <rect x="3" y="3" width="7" height="7" /><rect x="14" y="3" width="7" height="7" /><rect x="3" y="14" width="7" height="7" /><rect x="14" y="14" width="7" height="7" />
          </svg>
          Overview
        </NavLink>
        <NavLink
          to="/medicao"
          className={({ isActive }) =>
            `flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm transition-colors ${
              isActive
                ? 'bg-blue-50 text-blue-600 dark:bg-blue-950 dark:text-blue-400'
                : 'text-zinc-500 hover:bg-zinc-100 hover:text-zinc-900 dark:hover:bg-zinc-800 dark:hover:text-zinc-100'
            }`
          }
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M2 12h3l3-9 4 18 3-9h3"/>
          </svg>
          Medição
        </NavLink>
      </nav>

      {/* Usuário */}
      <div className="px-2 py-3 border-t border-zinc-200 dark:border-zinc-800">
        <div className="flex items-center gap-2 px-3 py-2 rounded-lg cursor-pointer hover:bg-zinc-100 dark:hover:bg-zinc-800">
          <div className="w-7 h-7 rounded-full bg-blue-600 flex items-center justify-center text-white text-xs font-medium flex-shrink-0">
            {usuario?.nome?.slice(0, 2).toUpperCase() ?? 'U'}
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100 truncate">
              {usuario?.nome ?? 'Usuário'}
            </p>
            <p className="text-[10px] text-zinc-500 truncate">
              {usuario?.nivel ?? '—'}
            </p>
          </div>
          <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="text-zinc-400">
            <polyline points="9 18 15 12 9 6" />
          </svg>
        </div>
      </div>
    </aside>
  )
}
