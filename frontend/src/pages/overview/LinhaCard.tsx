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
  onParadasSemMotivo?: (maquinaLinhaId: string) => void
  // sessaoId: com o filtro por data, a sessão daquele dia
  onAbrirMaquina?: (maquinaLinhaId: string, sessaoId?: string | null) => void
  // Colunas da tela inteira: o número de máquinas da linha mais comprida
  colunas: number
}

export default function LinhaCard({ linha, filtroAtivo, dataFiltro, onParadasSemMotivo, onAbrirMaquina, colunas }: Props) {
  const temSessao = linha.maquinas.some(m => m.sessaoAtiva)
  const temDados = filtroAtivo ? linha.maquinas.some(m => m.sessaoDia) : temSessao
  const dataTexto = dataFiltro ? `${dataFiltro.slice(8, 10)}/${dataFiltro.slice(5, 7)}/${dataFiltro.slice(0, 4)}` : ''
  const badgeText = filtroAtivo
    ? temDados
      ? `sessões de ${dataTexto}`
      : `sem sessão em ${dataTexto}`
    : temSessao
      ? 'em andamento'
      : 'sem sessão ativa'

  return (
    <div className={`${cardBase} overflow-hidden`}>
      {/* Header da linha */}
      <div className={cardHeader}>
        <div className="flex items-center gap-2">
          <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="var(--color-accent-600)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/>
          </svg>
          <span className="text-xs font-medium text-texto">{linha.nome}</span>
        </div>
        <span className="text-[10px] text-texto-3">{badgeText}</span>
      </div>

      {/* Fluxo de máquinas — sempre, mesmo sem sessão (cada card mostra sua situação) */}
      {linha.maquinas.length === 0 ? (
        <div className="px-4 py-5 text-xs text-texto-3">Nenhuma máquina nesta linha</div>
      ) : (
        <div className="p-3 flex items-stretch w-full gap-2">
          {[...linha.maquinas]
            .sort((a, b) => a.ordem - b.ordem)
            .map((maquina, index) => (
              <div key={maquina.id} className="flex items-stretch flex-1 basis-0 min-w-0">
                <div className="flex-1 min-w-0">
                <MaquinaCard
                  maquina={maquina}
                  filtroAtivo={filtroAtivo}
                  onParadasSemMotivo={onParadasSemMotivo ? () => onParadasSemMotivo(maquina.id) : undefined}
                  onAbrir={onAbrirMaquina && (!filtroAtivo || maquina.sessaoDia)
                    ? () => onAbrirMaquina(maquina.id, maquina.sessaoDia?.sessaoId)
                    : undefined}
                />
                </div>
                {/* Seta para a próxima máquina; a última guarda o mesmo espaço */}
                <div className="w-5 flex-shrink-0 flex items-center justify-center text-texto-4">
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