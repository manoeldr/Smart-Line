// Card individual de máquina no Overview.
// Mostra status ao vivo (bolinha colorida) quando há sessão ativa,
// ou "última sessão: [data]" com bolinha cinza quando a última sessão já foi finalizada.
// Administrador/Desenvolvedor veem um botão de Finalizar quando há sessão ativa.
// Máquina em coleta Semi Automática (WISE): etiqueta IoT, situação da coleta (inclusive sem
// comunicação e aguardando o WISE), motivo da parada atual e quantas paradas estão sem motivo.
// Todos os cards têm sempre o mesmo tamanho: as linhas opcionais ("sem motivo", Finalizar)
// guardam o espaço mesmo quando não aparecem, e a faixa da crítica é desenhada por dentro.
import { useAuth } from '../../contexts/AuthContext'
import type { MaquinaLinha } from '../../types'
import { badgeCritica, badgeIot, dotColorByStatus } from '../../styles/badges'
import { cardPaddedSm } from '../../styles/cards'

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
  if (maquina.situacaoColeta === 'SemComunicacao') {
    const min = maquina.semComunicacaoMinutos
    if (min == null) return 'Sem comunicação'
    if (min < 60) return `Sem comunicação há ${min} min`
    return `Sem comunicação há ${Math.floor(min / 60)}h ${min % 60}m`
  }
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
  const semMotivo = iot ? (maquina.paradasSemMotivo ?? 0) : 0
  // Sem comunicação: borda amarela (por dentro, sem mudar o tamanho do card)
  const semComunicacao = iot && maquina.situacaoColeta === 'SemComunicacao'
  const oeeColor = maquina.critica && !filtroAtivo ? 'text-blue-600 dark:text-blue-400' : 'text-zinc-900 dark:text-zinc-100'

  return (
    <div
      onClick={onAbrir}
      className={`h-full flex flex-col ${cardPaddedSm} cursor-pointer transition-colors hover:border-blue-400 dark:hover:border-blue-600 ${maquina.critica ? 'shadow-[inset_0_2px_0_0_#2563eb]' : ''} ${semComunicacao ? 'ring-2 ring-inset ring-amber-400 dark:ring-amber-500' : ''}`}
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
        <p className={`text-[10px] mb-2 truncate ${semComunicacao ? 'text-amber-600 dark:text-amber-400' : 'text-zinc-400'}`}>
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

      {/* Paradas da coleta automática ainda sem motivo (o espaço fica reservado) */}
      {!filtroAtivo && (
        <p className={`mt-1 text-[10px] text-amber-600 dark:text-amber-400 truncate ${semMotivo > 0 ? '' : 'invisible'}`}>
          {semMotivo > 1 ? `${semMotivo} paradas sem classificação` : '1 parada sem classificação'}
        </p>
      )}

      {/* Finalizar — só Admin/Desenvolvedor, só com sessão ativa (o espaço fica reservado) */}
      {!filtroAtivo && podeFinalizar && onFinalizar && (
        <div className="mt-auto pt-2">
        <button
          onClick={(e) => { e.stopPropagation(); onFinalizar() }}
          disabled={!maquina.sessaoAtiva}
          tabIndex={maquina.sessaoAtiva ? undefined : -1}
          className={`w-full text-[10px] text-red-600 dark:text-red-400 border border-red-200 dark:border-red-900 py-1 hover:bg-red-50 dark:hover:bg-red-950 transition-colors ${maquina.sessaoAtiva ? '' : 'invisible'}`}
        >
          {iot ? 'Finalizar coleta' : 'Finalizar sessão'}
        </button>
        </div>
      )}
    </div>
  )
}