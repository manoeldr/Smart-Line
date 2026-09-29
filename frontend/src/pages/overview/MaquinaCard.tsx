// Card individual de máquina no Overview.
// Mostra status ao vivo (bolinha colorida) quando há sessão ativa,
// ou "última sessão: [data]" com bolinha cinza quando a última sessão já foi finalizada.
// Administrador/Desenvolvedor veem um botão de Finalizar quando há sessão ativa.
// Máquina em coleta Semi Automática (WISE): etiqueta IoT, situação da coleta (inclusive sem
// comunicação e aguardando o WISE), motivo da parada atual e quantas paradas estão sem motivo.
import { useAuth } from '../../contexts/AuthContext'
import type { MaquinaLinha } from '../../types'
import { badgeCritica, badgeIot, dotColorByStatus } from '../../styles/badges'
import { cardPaddedSm, cardCritica } from '../../styles/cards'

interface Props {
  maquina: MaquinaLinha
  filtroAtivo: boolean
  onFinalizar?: () => void
  onAbrir?: () => void
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

// Texto de status de uma máquina em coleta automática.
function statusColeta(maquina: MaquinaLinha) {
  if (maquina.situacaoColeta === 'SemComunicacao') return 'Sem comunicação'
  if (maquina.situacaoColeta === 'AguardandoPrimeiraAmostra' || !maquina.situacaoColeta) return 'Aguardando WISE'
  const base = statusLabel[maquina.status] ?? '—'
  if (!maquina.status.startsWith('Parada')) return base
  return `${base} · ${maquina.motivoParadaAtual ?? 'sem motivo'}`
}

export default function MaquinaCard({ maquina, filtroAtivo, onFinalizar, onAbrir }: Props) {
  const { usuario } = useAuth()
  const podeFinalizar = usuario?.nivel === 'Administrador' || usuario?.nivel === 'Desenvolvedor'

  const temHistorico = !maquina.sessaoAtiva && maquina.ultimaSessaoFim
  const iot = !filtroAtivo && maquina.sessaoAtiva && !!maquina.acompanhamentoId
  const semDadosAoVivo = iot && maquina.situacaoColeta !== 'Rodando' && maquina.situacaoColeta !== 'Parada'
  const dotClass = filtroAtivo
    ? 'bg-blue-600'
    : temHistorico || semDadosAoVivo
      ? 'bg-zinc-400' // sessão finalizada, sem comunicação ou aguardando o WISE — bolinha cinza neutra
      : (dotColorByStatus[maquina.status] ?? 'bg-zinc-400')
  const oeeColor = maquina.critica && !filtroAtivo ? 'text-blue-600 dark:text-blue-400' : 'text-zinc-900 dark:text-zinc-100'

  return (
    <div
      onClick={onAbrir}
      className={`flex-1 min-w-0 ${cardPaddedSm} cursor-pointer transition-colors hover:border-blue-400 dark:hover:border-blue-600 ${cardCritica(maquina.critica)}`}
    >
      {/* Topo */}
      <div className="flex items-center justify-between mb-1.5">
        <div className={`w-2 h-2 rounded-full flex-shrink-0 ${dotClass}`} />
        <div className="flex items-center gap-1">
          {iot && <span className={badgeIot} title="Coleta Semi Automática (WISE)">IoT</span>}
          {maquina.critica && <span className={badgeCritica}>crítica</span>}
        </div>
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
            : iot
              ? statusColeta(maquina)
              : statusLabel[maquina.status] ?? '—'}
        </p>
      )}
      {filtroAtivo && <div className="mb-2" />}
      {/* OEE */}
      <p className={`text-base font-medium ${oeeColor}`}>
        {maquina.oee !== null ? `${maquina.oee}%` : '—'}
      </p>
      <p className="text-[10px] text-zinc-400">OEE</p>

      {/* Paradas da coleta automática ainda sem motivo */}
      {iot && (maquina.paradasSemMotivo ?? 0) > 0 && (
        <p className="mt-1 text-[10px] text-amber-600 dark:text-amber-400">
          {maquina.paradasSemMotivo} sem motivo
        </p>
      )}

      {/* Finalizar — só Admin/Desenvolvedor, só com sessão ativa */}
      {!filtroAtivo && maquina.sessaoAtiva && podeFinalizar && onFinalizar && (
        <button
          onClick={(e) => { e.stopPropagation(); onFinalizar() }}
          className="mt-2 w-full text-[10px] text-red-600 dark:text-red-400 border border-red-200 dark:border-red-900 py-1 hover:bg-red-50 dark:hover:bg-red-950 transition-colors"
        >
          {iot ? 'Finalizar coleta' : 'Finalizar sessão'}
        </button>
      )}
    </div>
  )
}