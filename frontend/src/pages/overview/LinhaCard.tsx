import type { Linha } from '../../types'
import MaquinaCard from './MaquinaCard'

interface Props {
  linha: Linha
  filtroAtivo: boolean
  dataFiltro: string | null
}

export default function LinhaCard({ linha, filtroAtivo, dataFiltro }: Props) {
  const temSessao = linha.maquinas.some(m => m.sessaoAtiva)
  const temDados = filtroAtivo ? linha.maquinas.some(m => m.oee !== null) : temSessao

  const badgeText = filtroAtivo
    ? temDados
      ? `sessão em ${dataFiltro}`
      : 'sem registros neste dia'
    : temSessao
      ? 'em andamento'
      : 'sem sessão ativa'

  return (
    <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 overflow-hidden">
      {/* Header da linha */}
      <div className="px-4 py-2.5 bg-zinc-50 dark:bg-zinc-800/50 border-b border-zinc-200 dark:border-zinc-800 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="#1961c0" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/>
          </svg>
          <span className="text-xs font-medium text-zinc-900 dark:text-zinc-100">{linha.nome}</span>
        </div>
        <span className="text-[10px] text-zinc-400">{badgeText}</span>
      </div>

      {/* Sem dados */}
      {!temDados ? (
        <div className="px-4 py-5 flex items-center gap-2 text-xs text-zinc-400">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
            <circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/>
          </svg>
          {filtroAtivo ? 'Nenhuma sessão registrada neste dia' : 'Nenhuma sessão ativa no momento'}
        </div>
      ) : (
        /* Fluxo de máquinas */
        <div className="p-3 flex items-stretch w-full gap-2">
          {linha.maquinas
            .sort((a, b) => a.ordem - b.ordem)
            .map((maquina, index) => (
              <div key={maquina.id} className="flex items-center flex-1 min-w-0">
                <MaquinaCard maquina={maquina} filtroAtivo={filtroAtivo} />
                {index < linha.maquinas.length - 1 && (
                  <div className="w-5 flex-shrink-0 flex items-center justify-center text-zinc-300 dark:text-zinc-600">
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <polyline points="9 18 15 12 9 6"/>
                    </svg>
                  </div>
                )}
              </div>
            ))}
        </div>
      )}
    </div>
  )
}