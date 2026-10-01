// Card de linha no Overview — agrupa os MaquinaCard de todas as máquinas daquela linha,
// exibidas em sequência (com setas indicando o fluxo de produção). Todas as linhas usam o
// mesmo número de colunas (o da linha com mais máquinas), para os cards terem sempre o mesmo
// tamanho de uma linha para outra.
import type { Linha } from '../../types'
import MaquinaCard from './MaquinaCard'
import { cardBase, cardHeader } from '../../styles/cards'

interface Props {
  linha: Linha
  filtroAtivo: boolean
  dataFiltro: string | null
  onFinalizarMaquina?: (maquinaLinhaId: string, maquinaNome: string, medeProducao: boolean) => void
  onAbrirMaquina?: (maquinaLinhaId: string) => void
  // Colunas da tela inteira: o número de máquinas da linha mais comprida
  colunas: number
}

export default function LinhaCard({ linha, filtroAtivo, dataFiltro, onFinalizarMaquina, onAbrirMaquina, colunas }: Props) {
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
    <div className={`${cardBase} overflow-hidden`}>
      {/* Header da linha */}
      <div className={cardHeader}>
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
          {[...linha.maquinas]
            .sort((a, b) => a.ordem - b.ordem)
            .map((maquina, index) => (
              <div key={maquina.id} className="flex items-stretch flex-1 basis-0 min-w-0">
                <div className="flex-1 min-w-0">
                <MaquinaCard
                  maquina={maquina}
                  filtroAtivo={filtroAtivo}
                  onFinalizar={onFinalizarMaquina ? () => onFinalizarMaquina(maquina.id, maquina.maquinaNome, maquina.medeProducao) : undefined}
                  onAbrir={onAbrirMaquina ? () => onAbrirMaquina(maquina.id) : undefined}
                />
                </div>
                {/* Seta para a próxima máquina; a última guarda o mesmo espaço */}
                <div className="w-5 flex-shrink-0 flex items-center justify-center text-zinc-300 dark:text-zinc-600">
                  {index < linha.maquinas.length - 1 && (
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <polyline points="9 18 15 12 9 6"/>
                    </svg>
                  )}
                </div>
              </div>
            ))}
          {/* Linha com menos máquinas: colunas vazias para os cards ficarem do mesmo tamanho */}
          {Array.from({ length: Math.max(0, colunas - linha.maquinas.length) }, (_, i) => (
            <div key={`vazio-${i}`} className="flex-1 basis-0 min-w-0" aria-hidden />
          ))}
        </div>
      )}
    </div>
  )
}