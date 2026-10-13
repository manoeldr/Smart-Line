// Menu lateral de navegação. Item "Configurações" só aparece para Administrador, Desenvolvedor e Auditor;
// "Desenvolvedor" (logs e simulador) só para o nível Desenvolvedor.
import { NavLink, useNavigate } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import Logo from '../Logo'

// Item de navegação padrão — reutiliza a mesma lógica de estilo ativo/inativo em todos os links
function navLinkClass({ isActive }: { isActive: boolean }) {
  return `flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm transition-colors ${
    isActive
      ? 'bg-accent-50 text-accent-600 dark:bg-accent-950 dark:text-accent-400'
      : 'text-texto-2 hover:bg-superficie-2 hover:text-texto'
  }`
}

export default function Sidebar() {
  const { usuario, logout } = useAuth()
  const navigate = useNavigate()
  const nivel = usuario?.nivel ?? ''
  const temConfiguracao = ['Administrador', 'Desenvolvedor', 'Auditor'].includes(nivel)
  const desenvolvedor = nivel === 'Desenvolvedor'

  function handleLogout() {
    logout()
    navigate('/login')
  }

  return (
    <aside className="w-52 bg-superficie border-r border-borda flex flex-col h-full flex-shrink-0">
      {/* Logo */}
      <div className="px-4 py-5 border-b border-borda flex items-center gap-3">
        <Logo tamanho={32} />
        <div>
          <p className="text-sm font-medium text-texto">Smart Line Scan</p>
          <p className="text-xs text-texto-2">Sanmartin</p>
        </div>
      </div>

      {/* Navegação */}
      <nav className="flex-1 px-2 py-3 flex flex-col">

        {/* Monitoramento */}
        <div>
          <p className="px-2 pb-1 text-[10px] font-medium uppercase tracking-widest text-texto-3">
            Monitoramento
          </p>
          <NavLink to="/medicao" className={navLinkClass}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M2 12h3l3-9 4 18 3-9h3"/>
            </svg>
            Medição
          </NavLink>
          <NavLink to="/overview" className={navLinkClass}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="3" y="3" width="7" height="7" /><rect x="14" y="3" width="7" height="7" /><rect x="3" y="14" width="7" height="7" /><rect x="14" y="14" width="7" height="7" />
            </svg>
            Overview
          </NavLink>
          <NavLink to="/dashboard" className={navLinkClass}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/>
            </svg>
            Dashboard
          </NavLink>
          <NavLink to="/relatorios" className={navLinkClass}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/>
            </svg>
            Relatórios
          </NavLink>
        </div>

        {/* Configurações — empurrado para baixo */}
        {temConfiguracao && (
          <div className="mt-auto">
            <p className="px-2 pb-1 text-[10px] font-medium uppercase tracking-widest text-texto-3">
              Sistema
            </p>
            <NavLink to="/configuracao" className={navLinkClass}>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/>
              </svg>
              Configurações
            </NavLink>
            {desenvolvedor && (
              <NavLink to="/desenvolvedor" className={navLinkClass}>
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <polyline points="16 18 22 12 16 6"/><polyline points="8 6 2 12 8 18"/>
                </svg>
                Desenvolvedor
              </NavLink>
            )}
          </div>
        )}
      </nav>

      {/* Usuário */}
      <div className="px-2 py-3 border-t border-borda">
        <div className="flex items-center gap-2 px-3 py-2 rounded-lg">
          <div className="w-7 h-7 rounded-full bg-accent-600 flex items-center justify-center text-accent-fg text-xs font-medium flex-shrink-0">
            {usuario?.nome?.slice(0, 2).toUpperCase() ?? 'U'}
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-xs font-medium text-texto truncate">
              {usuario?.nome ?? 'Usuário'}
            </p>
            <p className="text-[10px] text-texto-2 truncate">
              {usuario?.nivel ?? '—'}
            </p>
          </div>
          <button
            onClick={handleLogout}
            title="Sair"
            className="text-texto-3 hover:text-red-500 transition-colors flex-shrink-0"
          >
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/>
            </svg>
          </button>
        </div>
      </div>
    </aside>
  )
}