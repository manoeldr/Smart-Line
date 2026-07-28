// Card de resumo de OEE por máquina no Dashboard, agregando dados de todas as sessões
// finalizadas dentro do período selecionado. Ao clicar, abre o MaquinaDetalheModal.
import type { MaquinaDashboardDto } from '../../services/dashboardService'
import { badgeCritica } from '../../styles/badges'
import { cardPadded, cardCritica } from '../../styles/cards'

interface Props {
  dados: MaquinaDashboardDto
}

function formatarHoras(ms: number) {
  const horas = ms / 3600000
  return horas.toFixed(1) + 'h'
}

export default function MaquinaDashboardCard({ dados }: Props) {
  const oeeColor = dados.oee >= 85
    ? 'text-green-600 dark:text-green-400'
    : dados.oee >= 60
      ? 'text-amber-600 dark:text-amber-400'
      : 'text-red-600 dark:text-red-400'

  return (
    <div className={`${cardPadded} ${cardCritica(dados.critica)}`}>

      {/* Header */}
      <div className="flex items-center justify-between mb-3">
        <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">{dados.maquinaNome}</p>
        {dados.critica && <span className={badgeCritica}>crítica</span>}
      </div>

      {/* OEE grande */}
      <div className="text-center mb-4">
        <p className={`text-3xl font-medium ${oeeColor}`}>{dados.oee}%</p>
        <p className="text-[10px] text-zinc-400">OEE médio</p>
      </div>

      {/* Disponibilidade / Performance / Qualidade */}
      <div className="grid grid-cols-3 gap-2 mb-4">
        <div className="text-center">
          <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">{dados.disponibilidade}%</p>
          <p className="text-[9px] text-zinc-400">Disponib.</p>
        </div>
        <div className="text-center">
          <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">{dados.performance}%</p>
          <p className="text-[9px] text-zinc-400">Perform.</p>
        </div>
        <div className="text-center">
          <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">{dados.qualidade}%</p>
          <p className="text-[9px] text-zinc-400">Qualid.</p>
        </div>
      </div>

      {/* Produção / Refugo */}
      <div className="grid grid-cols-2 gap-2 mb-3 pt-3 border-t border-zinc-100 dark:border-zinc-800">
        <div>
          <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">{dados.producao.toLocaleString('pt-BR')}</p>
          <p className="text-[9px] text-zinc-400">Produção total</p>
        </div>
        <div>
          <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">{dados.refugo.toLocaleString('pt-BR')}</p>
          <p className="text-[9px] text-zinc-400">Refugo total</p>
        </div>
      </div>

      {/* Tempo rodando / parado / sessões */}
      <div className="grid grid-cols-3 gap-2 text-[9px] text-zinc-400 pt-2 border-t border-zinc-100 dark:border-zinc-800">
        <div>
          <p className="text-zinc-900 dark:text-zinc-100 text-xs font-medium">{formatarHoras(dados.tempoRodandoMs)}</p>
          rodando
        </div>
        <div>
          <p className="text-zinc-900 dark:text-zinc-100 text-xs font-medium">{formatarHoras(dados.tempoParadoMs)}</p>
          parado
        </div>
        <div>
          <p className="text-zinc-900 dark:text-zinc-100 text-xs font-medium">{dados.numSessoes}</p>
          sessões
        </div>
      </div>
    </div>
  )
}