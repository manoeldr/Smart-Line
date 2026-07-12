import type { MaquinaLinha } from '../../types'

interface Props {
  maquina: MaquinaLinha
  filtroAtivo: boolean
}

const statusDot: Record<string, string> = {
  Rodando:         'bg-green-600',
  ParadaInterna:   'bg-red-500',
  ParadaExterna:   'bg-yellow-400',
  ParadaPlanejada: 'bg-orange-500',
  SemSessao:       'bg-zinc-400',
}

const statusLabel: Record<string, string> = {
  Rodando:         'Rodando',
  ParadaInterna:   'Parada interna',
  ParadaExterna:   'Parada externa',
  ParadaPlanejada: 'Parada planejada',
  SemSessao:       'Sem sessão ativa',
}

export default function MaquinaCard({ maquina, filtroAtivo }: Props) {
  const dotClass = filtroAtivo ? 'bg-blue-600' : (statusDot[maquina.status] ?? 'bg-zinc-400')
  const oeeColor = maquina.critica && !filtroAtivo ? 'text-blue-600 dark:text-blue-400' : 'text-zinc-900 dark:text-zinc-100'

  return (
    <div className={`flex-1 min-w-0 bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 p-3 cursor-pointer transition-colors hover:border-blue-400 dark:hover:border-blue-600 ${maquina.critica ? 'border-t-2 border-t-blue-600' : ''}`}>
      {/* Topo */}
      <div className="flex items-center justify-between mb-1.5">
        <div className={`w-2 h-2 rounded-full flex-shrink-0 ${dotClass}`} />
        {maquina.critica && (
          <span className="text-[9px] font-medium text-blue-600 dark:text-blue-400 bg-blue-50 dark:bg-blue-950 px-1.5 py-0.5">
            crítica
          </span>
        )}
      </div>

      {/* Nome */}
      <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100 truncate mb-0.5">
        {maquina.maquinaNome}
      </p>

      {/* Status */}
      {!filtroAtivo && (
        <p className="text-[10px] text-zinc-400 mb-2 truncate">
          {statusLabel[maquina.status] ?? '—'}
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