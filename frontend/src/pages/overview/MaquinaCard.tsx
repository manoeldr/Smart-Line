// Card individual de máquina no Overview.
// Mostra status ao vivo (bolinha colorida) quando há sessão ativa,
// ou "última sessão: [data]" com bolinha cinza quando a última sessão já foi finalizada.
// Administrador/Desenvolvedor veem um botão de Finalizar quando há sessão ativa.
// Máquina em coleta IoT (WISE): etiqueta IoT, situação da coleta (inclusive sem
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

// "1h 25m", como no detalhe da máquina.
function formatarHoras(ms: number) {
  const horas = Math.floor(ms / 3600000)
  const minutos = Math.floor((ms % 3600000) / 60000)
  return `${horas}h ${minutos}m`
}

function hora(dataIso: string) {
  return new Date(dataIso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
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

function Numero({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div className="min-w-0">
      <p className="text-texto font-medium truncate">{valor}</p>
      <p className="text-texto-3">{rotulo}</p>
    </div>
  )
}

export default function MaquinaCard({ maquina, filtroAtivo, onFinalizar, onAbrir }: Props) {
  const { usuario } = useAuth()
  const podeFinalizar = usuario?.nivel === 'Administrador' || usuario?.nivel === 'Desenvolvedor'

  const temHistorico = !maquina.sessaoAtiva && maquina.ultimaSessaoFim
  const iot = !filtroAtivo && maquina.sessaoAtiva && !!maquina.acompanhamentoId
  const semDadosAoVivo = iot && maquina.situacaoColeta !== 'Rodando' && maquina.situacaoColeta !== 'Parada'
  const dotClass = filtroAtivo
    ? 'bg-accent-600'
    : temHistorico || semDadosAoVivo
      ? 'bg-texto-3' // sessão finalizada, sem comunicação ou aguardando o WISE — bolinha cinza neutra
      : (dotColorByStatus[maquina.status] ?? 'bg-texto-3')
  const semMotivo = iot ? (maquina.paradasSemMotivo ?? 0) : 0
  // Sem comunicação: borda amarela (por dentro, sem mudar o tamanho do card)
  const semComunicacao = iot && maquina.situacaoColeta === 'SemComunicacao'
  const oeeColor = maquina.critica && !filtroAtivo ? 'text-accent-600 dark:text-accent-400' : 'text-texto'

  return (
    <div
      onClick={onAbrir}
      className={`h-full flex flex-col ${cardPaddedSm} cursor-pointer transition-colors hover:border-accent-400 dark:hover:border-accent-600 ${maquina.critica ? 'shadow-[inset_0_2px_0_0_var(--color-accent-600)]' : ''} ${semComunicacao ? 'ring-2 ring-inset ring-amber-400 dark:ring-amber-500' : ''}`}
    >
      {/* Topo */}
      <div className="flex items-center justify-between mb-1.5">
        <div className={`w-2 h-2 rounded-full flex-shrink-0 ${dotClass}`} />
        <div className="flex items-center gap-1">
          {maquina.critica && <span className={badgeCritica}>Crítica</span>}
          {iot && <span className={badgeIot} title="Coleta IoT (WISE)">IoT</span>}
        </div>
      </div>
      {/* Nome */}
      <p className="text-xs font-medium text-texto truncate mb-0.5">
        {maquina.maquinaNome}
      </p>
      {/* Status: última sessão (finalizada) prevalece sobre o status ao vivo */}
      {!filtroAtivo && (
        <p className={`text-[10px] mb-2 truncate ${semComunicacao ? 'text-amber-600 dark:text-amber-400' : 'text-texto-3'}`}>
          {temHistorico
            ? `última sessão: ${formatarUltimaSessao(maquina.ultimaSessaoFim!)}`
            : iot
              ? statusColeta(maquina)
              : statusLabel[maquina.status] ?? '—'}
        </p>
      )}
      {/* Filtro por data: a sessão daquele dia (início e fim) */}
      {filtroAtivo && (
        <p className="text-[10px] text-texto-3 mb-2 truncate">
          {maquina.sessaoDia
            ? `sessão ${hora(maquina.sessaoDia.inicio)} – ${maquina.sessaoDia.fim ? hora(maquina.sessaoDia.fim) : 'em andamento'}`
            : 'sem sessão neste dia'}
        </p>
      )}
      {/* OEE */}
      <p className={`text-base font-medium ${oeeColor}`}>
        {maquina.oee !== null ? `${maquina.oee}%` : '—'}
      </p>
      <p className="text-[10px] text-texto-3">OEE</p>

      {/* Filtro por data: os números da sessão daquele dia */}
      {filtroAtivo && maquina.sessaoDia && (
        <div className="grid grid-cols-2 gap-x-2 gap-y-1 mt-2 pt-2 border-t border-borda-suave text-[10px]">
          <Numero rotulo="Produção" valor={maquina.sessaoDia.producao.toLocaleString('pt-BR')} />
          <Numero rotulo="Refugo" valor={maquina.sessaoDia.refugo.toLocaleString('pt-BR')} />
          <Numero rotulo="Rodando" valor={formatarHoras(maquina.sessaoDia.tempoRodandoMs)} />
          <Numero rotulo="Parado" valor={formatarHoras(maquina.sessaoDia.tempoParadoMs)} />
        </div>
      )}

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