// Modal de detalhes de uma máquina, aberto ao clicar num card do Dashboard.
// Mostra métricas da última sessão (ativa ou finalizada), gráfico dinâmico por hora
// (produção em barra + campos extras selecionáveis em linha) e linha do tempo de eventos Marcha/Parada.
// Só a linha do tempo tem scroll próprio — o resto (métricas, gráfico) fica fixo.
import { useEffect, useRef, useState } from 'react'
import {
  ComposedChart, Bar, Cell, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer
} from 'recharts'
import { sessaoDetalheService, type SessaoDetalheDto } from '../services/sessaoDetalheService'
import { modalOverlayDark, modalPanel, modalHeader, modalTitle, modalSubtitle } from '../styles/modals'
import { badgeAtivaVerde } from '../styles/badges'
import { metricaBox, metricaValor, metricaLabel } from '../styles/cards'
import PainelColetaIot from '../components/iot/PainelColetaIot'
import EditarMotivoParadaModal from './EditarMotivoParadaModal'
import HistoricoParadaModal from './HistoricoParadaModal'
import { useAuth } from '../contexts/AuthContext'

interface Props {
  open: boolean
  maquinaLinhaId: string | null
  onFechar: () => void
  // Chamado quando a coleta Semi Automática é finalizada por aqui (quem abriu recarrega a tela)
  onColetaFinalizada?: () => void
}

function formatarHoras(ms: number) {
  const horas = Math.floor(ms / 3600000)
  const minutos = Math.floor((ms % 3600000) / 60000)
  return `${horas}h ${minutos}m`
}

function formatarHora(dataIso: string) {
  return new Date(dataIso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
}

function formatarDataHora(dataIso: string) {
  return new Date(dataIso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })
}

const CORES_LINHA = ['#3b82f6', '#f59e0b', '#10b981', '#ef4444', '#8b5cf6', '#ec4899']

// As métricas vêm do que está gravado, e a coleta Semi Automática grava a produção a cada
// 5 min, nos múltiplos do relógio (10:00, 10:05...). O modal aberto recarrega logo depois de
// cada gravação (10 s de folga), sem piscar.
const INTERVALO_GRAVACAO_MS = 5 * 60 * 1000
const FOLGA_MS = 10 * 1000

function msAteProximaAtualizacao(agora = Date.now()) {
  const proxima = Math.ceil((agora - FOLGA_MS) / INTERVALO_GRAVACAO_MS) * INTERVALO_GRAVACAO_MS + FOLGA_MS
  return Math.max(1000, proxima - agora)
}

export default function MaquinaDetalheModal({ open, maquinaLinhaId, onFechar, onColetaFinalizada }: Props) {
  const [dados, setDados] = useState<SessaoDetalheDto | null>(null)
  const [loading, setLoading] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [camposSelecionados, setCamposSelecionados] = useState<Set<string>>(new Set())

  // Editar o motivo de uma parada da linha do tempo (Manual ou Semi Automático) e ver o histórico
  const { usuario } = useAuth()
  const podeEditarMotivo = ['Administrador', 'Desenvolvedor', 'Auditor'].includes(usuario?.nivel ?? '')
  const [editandoParada, setEditandoParada] = useState<string | null>(null)
  const [historicoParada, setHistoricoParada] = useState<string | null>(null)
  const [recarregar, setRecarregar] = useState(0)

  // Máquina cujos dados estão na tela: "Carregando..." só ao abrir (ou trocar de máquina).
  // As atualizações depois disso (a cada gravação, ou depois de editar um motivo) trocam
  // os números no lugar, sem esconder o conteúdo.
  const carregadoPara = useRef<string | null>(null)

  useEffect(() => {
    if (!open || !maquinaLinhaId) {
      carregadoPara.current = null
      return
    }
    let ativo = true
    let timer: ReturnType<typeof setTimeout> | undefined

    async function carregar() {
      const primeira = carregadoPara.current !== maquinaLinhaId
      if (primeira) {
        setLoading(true)
        setErro(null)
        setDados(null)
      }
      try {
        const data = await sessaoDetalheService.getUltimaSessaoDetalhe(maquinaLinhaId!)
        if (!ativo) return
        setDados(data)
        setErro(null)
        if (primeira) setCamposSelecionados(new Set())
        carregadoPara.current = maquinaLinhaId
      } catch (e: unknown) {
        // Numa atualização em segundo plano, uma falha só mantém o que já está na tela.
        if (!ativo || !primeira) return
        const mensagem = e instanceof Error ? e.message : ''
        if (mensagem.includes('404')) {
          setErro(null)
        } else {
          setErro(mensagem || 'Erro ao carregar dados')
        }
      } finally {
        if (ativo) {
          if (primeira) setLoading(false)
          timer = setTimeout(carregar, msAteProximaAtualizacao())
        }
      }
    }

    carregar()
    return () => { ativo = false; clearTimeout(timer) }
  }, [open, maquinaLinhaId, recarregar])

  function toggleCampo(id: string) {
    setCamposSelecionados(prev => {
      const novo = new Set(prev)
      if (novo.has(id)) novo.delete(id)
      else novo.add(id)
      return novo
    })
  }

  async function abrirFoto(fotoPath: string) {
    try {
      const token = localStorage.getItem('token')
      const res = await fetch(`/api/paradas/foto/${fotoPath}`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      })
      if (!res.ok) throw new Error('Erro ao carregar foto')
      const blob = await res.blob()
      const url = window.URL.createObjectURL(blob)
      window.open(url, '_blank')
    } catch {
      alert('Erro ao carregar a foto.')
    }
  }

  if (!open) return null

  const dadosGrafico = (() => {
    if (!dados) return []
    const horarios = new Set<string>()
    dados.pontosProducao.forEach(p => horarios.add(p.hora))
    dados.camposExtras.forEach(c => c.pontos.forEach(p => horarios.add(p.hora)))

    const horariosOrdenados = Array.from(horarios).sort()

    // Semi Automático: cada barra fica no início da hora (14:00 = das 14:00 às 14:59). A da hora
    // ainda em andamento (marcada pela API) tem o parcial até agora: aparece tracejada e mais
    // clara, e a dica explica.

    return horariosOrdenados.map(hora => {
      const producaoPonto = dados.pontosProducao.find(p => p.hora === hora)
      const ponto: Record<string, string | number | boolean> = {
        hora: formatarHora(hora),
        fimDaHora: formatarHora(new Date(new Date(hora).getTime() + 3600000).toISOString()),
        parcial: producaoPonto?.parcial === true,
      }
      if (producaoPonto) ponto['Produção'] = producaoPonto.quantidade

      dados.camposExtras.forEach(campo => {
        const extraPonto = campo.pontos.find(p => p.hora === hora)
        if (extraPonto) ponto[campo.nome] = extraPonto.valor
      })

      return ponto
    })
  })()

  return (
    <div className={modalOverlayDark}>
      <div className={`${modalPanel} w-[900px] max-h-[90vh]`}>

        {/* Header — fixo */}
        <div className={`${modalHeader} flex items-center justify-between flex-shrink-0`}>
          <div>
            <p className={modalTitle}>{dados?.maquinaNome ?? 'Detalhes da máquina'}</p>
            {dados && (
              <p className={modalSubtitle}>
                {formatarDataHora(dados.inicio)} {dados.fim ? `— ${formatarDataHora(dados.fim)}` : '— em andamento'}
                {dados.status === 'EmAndamento' && <span className={`ml-2 ${badgeAtivaVerde}`}>ativa</span>}
              </p>
            )}
          </div>
          <button onClick={onFechar} className="text-zinc-400 hover:text-zinc-900 dark:hover:text-zinc-100">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
          </button>
        </div>

        {/* Coleta Semi Automática em andamento (some quando a máquina não está em coleta) */}
        {maquinaLinhaId && (
          <PainelColetaIot
            maquinaLinhaId={maquinaLinhaId}
            onFinalizada={() => { onColetaFinalizada?.(); onFechar() }}
          />
        )}

        {loading ? (
          <p className="text-xs text-zinc-400 text-center py-8">Carregando...</p>
        ) : erro ? (
          <p className="text-xs text-red-400 text-center py-8">{erro}</p>
        ) : !dados ? (
          <p className="text-xs text-zinc-400 text-center py-8">Nenhuma sessão registrada para esta máquina</p>
        ) : (
          <>
            {/* Conteúdo fixo — métricas, gráfico e seletor */}
            <div className="px-5 py-4 flex-shrink-0">
              {/* Grid de métricas */}
              <div className="grid grid-cols-5 gap-2 mb-6">
                <MetricaCard label="OEE" valor={`${dados.oee}%`} destaque />
                <MetricaCard label="Eficiência" valor={`${dados.eficiencia}%`} />
                <MetricaCard label="Disponibilidade" valor={`${dados.disponibilidade}%`} />
                <MetricaCard label="Qualidade" valor={`${dados.qualidade}%`} />
                <MetricaCard label="Tempo Rodando" valor={formatarHoras(dados.tempoRodandoMs)} />
                <MetricaCard label="Tempo Parado" valor={formatarHoras(dados.tempoParadoMs)} />
                <MetricaCard label="Produção Total" valor={dados.producao.toLocaleString('pt-BR')} />
                <MetricaCard label="Refugo Total" valor={dados.refugo.toLocaleString('pt-BR')} />
                <MetricaCard label="MTTR" valor={dados.mttrMs !== null ? formatarHoras(dados.mttrMs) : '—'} />
                <MetricaCard label="MTBF" valor={dados.mtbfMs !== null ? formatarHoras(dados.mtbfMs) : '—'} />
              </div>

              {/* Seletor de campos do gráfico */}
              <div className="mb-3">
                <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100 mb-2">Gráfico por hora</p>
                <div className="flex flex-wrap gap-2">
                  <span className="text-[10px] px-2 py-1 bg-blue-50 dark:bg-blue-950 text-blue-600 dark:text-blue-400">Produção (sempre visível)</span>
                  {dados.camposExtras.map((campo, i) => (
                    <button
                      key={campo.campoMaquinaId}
                      onClick={() => toggleCampo(campo.campoMaquinaId)}
                      className={`text-[10px] px-2 py-1 border transition-colors ${
                        camposSelecionados.has(campo.campoMaquinaId)
                          ? 'text-white border-transparent'
                          : 'border-zinc-200 dark:border-zinc-700 text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800'
                      }`}
                      style={camposSelecionados.has(campo.campoMaquinaId) ? { backgroundColor: CORES_LINHA[i % CORES_LINHA.length] } : {}}
                    >
                      {campo.nome}{campo.unidade ? ` (${campo.unidade})` : ''}
                    </button>
                  ))}
                </div>
              </div>

              {/* Gráfico */}
              <div className="h-64">
                <ResponsiveContainer width="100%" height="100%">
                  <ComposedChart data={dadosGrafico}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#e4e4e7" />
                    <XAxis dataKey="hora" tick={{ fontSize: 10 }} />
                    <YAxis yAxisId="left" tick={{ fontSize: 10 }} />
                    <YAxis yAxisId="right" orientation="right" tick={{ fontSize: 10 }} />
                    <Tooltip
                      labelFormatter={(rotulo, itens) => {
                        const ponto = itens?.[0]?.payload as Record<string, string | number | boolean> | undefined
                        return ponto?.parcial
                          ? `Em andamento: ${rotulo}–${ponto.fimDaHora} · parcial, atualiza a cada 5 min`
                          : rotulo
                      }}
                    />
                    <Legend wrapperStyle={{ fontSize: 11 }} />
                    <Bar yAxisId="left" dataKey="Produção" fill="#1961c0">
                      {dadosGrafico.map((ponto, i) => (
                        <Cell
                          key={i}
                          fill={ponto.parcial ? 'rgba(25, 97, 192, 0.3)' : '#1961c0'}
                          stroke={ponto.parcial ? '#1961c0' : 'none'}
                          strokeWidth={ponto.parcial ? 1.5 : 0}
                          strokeDasharray={ponto.parcial ? '4 3' : undefined}
                        />
                      ))}
                    </Bar>
                    {dados.camposExtras
                      .filter(c => camposSelecionados.has(c.campoMaquinaId))
                      .map((campo, i) => (
                        <Line
                          key={campo.campoMaquinaId}
                          yAxisId="right"
                          type="monotone"
                          dataKey={campo.nome}
                          stroke={CORES_LINHA[i % CORES_LINHA.length]}
                          strokeWidth={2}
                        />
                      ))}
                  </ComposedChart>
                </ResponsiveContainer>
              </div>
            </div>

            {/* Timeline de eventos — única parte com scroll */}
            <div className="flex-1 overflow-y-auto px-5 pb-4 min-h-0">
              <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100 mb-2 sticky top-0 bg-white dark:bg-zinc-900 py-1">Linha do tempo</p>
              <div className="flex flex-col">
                {dados.eventos.map((evento, i) => (
                  <div key={i} className="flex items-start gap-3 py-2 border-b border-zinc-100 dark:border-zinc-800 last:border-0">
                    <div className={`w-2 h-2 rounded-full mt-1 flex-shrink-0 ${evento.tipo === 'Marcha' ? 'bg-green-600' : 'bg-red-500'}`} />
                    <div className="flex-1 flex items-start justify-between gap-2">
                      <div>
                        <div className="flex items-center gap-2">
                          <span className={`text-xs font-medium ${evento.tipo === 'Marcha' ? 'text-green-700 dark:text-green-400' : 'text-red-700 dark:text-red-400'}`}>
                            {evento.tipo}
                          </span>
                          <span className="text-[10px] text-zinc-400">{formatarDataHora(evento.horario)}</span>
                        </div>
                        {evento.tipo === 'Parada' && (
                          <p className="text-[11px] text-zinc-500 mt-0.5">
                            {evento.motivoNome ?? <span className="text-amber-600 dark:text-amber-400">sem motivo (conta como interna)</span>}
                            {evento.duracaoMs !== null ? ` — ${formatarHoras(evento.duracaoMs!)}` : ' — em andamento'}
                          </p>
                        )}
                        {evento.tipo === 'Parada' && evento.paradaId && (
                          <div className="flex items-center gap-3 mt-1">
                            {podeEditarMotivo && dados.maquinaId && (
                              <button onClick={() => setEditandoParada(evento.paradaId!)} className="text-[10px] text-blue-600 dark:text-blue-400 hover:underline">
                                Editar motivo
                              </button>
                            )}
                            <button onClick={() => setHistoricoParada(evento.paradaId!)} className="text-[10px] text-zinc-400 hover:text-zinc-700 dark:hover:text-zinc-200 hover:underline">
                              Histórico
                            </button>
                          </div>
                        )}
                      </div>
                      {evento.fotoPath && (
                        <button
                          onClick={() => abrirFoto(evento.fotoPath!)}
                          title="Ver foto da parada"
                          className="flex-shrink-0 text-zinc-400 hover:text-blue-600 dark:hover:text-blue-400 transition-colors"
                        >
                          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z"/><circle cx="12" cy="13" r="4"/>
                          </svg>
                        </button>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </>
        )}
      </div>

      <EditarMotivoParadaModal
        alvo={editandoParada && dados?.maquinaId ? { paradaId: editandoParada, maquinaId: dados.maquinaId } : null}
        onFechar={() => setEditandoParada(null)}
        onSalvo={() => { setEditandoParada(null); setRecarregar(r => r + 1) }}
      />
      <HistoricoParadaModal paradaId={historicoParada} onFechar={() => setHistoricoParada(null)} />
    </div>
  )
}

function MetricaCard({ label, valor, destaque }: { label: string; valor: string; destaque?: boolean }) {
  return (
    <div className={metricaBox}>
      <p className={`${metricaValor} ${destaque ? 'text-blue-600 dark:text-blue-400' : ''}`}>
        {valor}
      </p>
      <p className={metricaLabel}>{label}</p>
    </div>
  )
}