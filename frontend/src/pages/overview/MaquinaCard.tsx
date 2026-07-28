// Card individual de máquina no Overview.
// Mostra status ao vivo (bolinha colorida) quando há sessão ativa,
// ou "última sessão: [data]" com bolinha cinza quando a última sessão já foi finalizada.
import type { MaquinaLinha } from '../../types'
import { badgeCritica, dotColorByStatus } from '../../styles/badges'
import { cardPaddedSm, cardCritica } from '../../styles/cards'

interface Props {
  maquina: MaquinaLinha
  filtroAtivo: boolean
}

const statusLabel: Record<string, string> = {
  Rodando:         'Rodando',
  ParadaInterna:   'Parada interna',
  ParadaExterna:   'Parada externa',
  ParadaPlanejada: 'Parada planejada',
  SemSessao:       'Sem sessão ativa',
}

function formatarUltimaSessao(dataIso: string) {
  const data = new Date(dataIso)
  const dia = data.toLocaleDateString('pt-BR')
  const hora = data.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
  return `${dia} ${hora}`
}

export default function MaquinaCard({ maquina, filtroAtivo }: Props) {
  const temHistorico = !maquina.sessaoAtiva && maquina.ultimaSessaoFim
  const dotClass = filtroAtivo
    ? 'bg-blue-600'
    : temHistorico
      ? 'bg-zinc-400' // sessão finalizada — bolinha cinza neutra
      : (dotColorByStatus[maquina.status] ?? 'bg-zinc-400')
  const oeeColor = maquina.critica && !filtroAtivo ? 'text-blue-600 dark:text-blue-400' : 'text-zinc-900 dark:text-zinc-100'

  return (
    <div className={`flex-1 min-w-0 ${cardPaddedSm} cursor-pointer transition-colors hover:border-blue-400 dark:hover:border-blue-600 ${cardCritica(maquina.critica)}`}>
      {/* Topo */}
      <div className="flex items-center justify-between mb-1.5">
        <div className={`w-2 h-2 rounded-full flex-shrink-0 ${dotClass}`} />
        {maquina.critica && <span className={badgeCritica}>crítica</span>}
      </div>

      {/* Nome */}
      <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100 truncate mb-0.5">
        {maquina.maquinaNome}
      </p>

      {/* Status: última sessão (finalizada) prevalece sobre o status ao vivo */}
      {!filtroAtivo && (
        <p className="text-[10px] text-zinc-400 mb-2 truncate">
          {temHistorico
            ? `última sessão: ${formatarUltimaSessao(maquina.ultimaSessaoFim!)}`
            : statusLabel[maquina.status] ?? '—'}
        </p>
      )}
      {filtroAtivo && <div className="mb-2" />}

      {/* OEE */}
      <p className={`text-base font-medium ${oeeColor}`}>
        {maquina.oee !== null ? `${maquina.oee}%` : '—'}
      </p>
      <p className="text-[10px] text-zinc-400">OEE</p>
    </div>
  )
}