// Modal de detalhes de uma máquina, aberto ao clicar num card do Dashboard.
// Mostra métricas da última sessão (ativa ou finalizada), gráfico dinâmico por hora
// (produção em barra + campos extras selecionáveis em linha) e linha do tempo de eventos Marcha/Parada.
// Só a linha do tempo tem scroll próprio — o resto (métricas, gráfico) fica fixo.
import { useEffect, useRef, useState } from 'react'
import {
  BarChart, ComposedChart, Bar, Cell, LabelList, Line, ReferenceLine, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer
} from 'recharts'
import { fetchAutenticado } from '../services/api'
import { sessaoDetalheService, type EventoTimelineDto, type ParadaPorHoraDto, type ParadaPorMotivoDto, type SessaoDetalheDto } from '../services/sessaoDetalheService'
import { modalOverlayDark, modalPanel, modalHeader, modalTitle, modalSubtitle } from '../styles/modals'
import { badgeAtivaVerde } from '../styles/badges'
import { metricaBox, metricaValor, metricaLabel } from '../styles/cards'
import { areaGrafico } from '../styles/graficos'
import PainelColetaIot from '../components/iot/PainelColetaIot'
import EditarMotivoParadaModal from './EditarMotivoParadaModal'
import HistoricoParadaModal from './HistoricoParadaModal'
import { useAuth } from '../contexts/AuthContext'
import { useParametroUrl } from '../hooks/useParametroUrl'

interface Props {
  open: boolean
  maquinaLinhaId: string | null
  // Uma sessão específica (a do dia escolhido no filtro do Overview); nula = a última da máquina
  sessaoId?: string | null
  onFechar: () => void
  // Chamado quando a coleta IoT é finalizada por aqui (quem abriu recarrega a tela)
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

// Horário com segundos; com a data quando não é o mesmo dia de referência.
function formatarHorario(data: Date, referencia?: Date) {
  const hora = data.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit', second: '2-digit' })
  return referencia && data.toDateString() !== referencia.toDateString()
    ? `${data.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' })} ${hora}`
    : hora
}

// Duração legível: "45 s", "7 min 28 s", "1 h 05 min".
function formatarDuracao(ms: number) {
  const s = Math.max(0, Math.round(ms / 1000))
  if (s < 60) return `${s} s`
  const min = Math.floor(s / 60)
  if (min < 60) return `${min} min ${String(s % 60).padStart(2, '0')} s`
  return `${Math.floor(min / 60)} h ${String(min % 60).padStart(2, '0')} min`
}

// Produção em azul (o mesmo do relatório), em qualquer tema; velocidade nominal em vermelho
const COR_PRODUCAO = '#1961ac'
const COR_NOMINAL = '#ef4444'
const CORES_LINHA = ['#3b82f6', '#f59e0b', '#10b981', '#ef4444', '#8b5cf6', '#ec4899']

// Produção da hora dentro da barra, na vertical, em branco; barra baixa demais: acima dela, em cinza
function RotuloProducao(props: { x?: number | string; y?: number | string; width?: number | string; height?: number | string; value?: unknown }) {
  const x = Number(props.x), y = Number(props.y), w = Number(props.width), h = Number(props.height)
  const valor = Number(props.value)
  if (!Number.isFinite(valor) || valor <= 0 || !Number.isFinite(x) || !Number.isFinite(w)) return null
  const texto = valor.toLocaleString('pt-BR')
  const dentro = h >= texto.length * 6.5 + 8
  const cx = x + w / 2
  return dentro ? (
    <text x={cx} y={y + h / 2} textAnchor="middle" dominantBaseline="central" fontSize={10} fontWeight={600} fill="#ffffff" transform={`rotate(-90 ${cx} ${y + h / 2})`}>
      {texto}
    </text>
  ) : (
    <text x={cx} y={y - 3} textAnchor="middle" fontSize={10} fontWeight={600} fill="#71717a">
      {texto}
    </text>
  )
}

// Dica do gráfico de produção: produção, sem comunicação e a soma (produção bruta) quando há
// as duas; depois os campos extras.
function DicaProducao({ active, payload, label }: {
  active?: boolean
  payload?: { name?: string; value?: number | string; color?: string; payload?: Record<string, string | number | boolean> }[]
  label?: string | number
}) {
  if (!active || !payload?.length) return null
  const ponto = payload[0].payload ?? {}
  const producao = Number(ponto['Produção'] ?? 0)
  const semComunicacao = Number(ponto['Sem comunicação'] ?? 0)
  const titulo = ponto.parcial ? `Em andamento: ${label}–${ponto.fimDaHora} · parcial, atualiza a cada 5 min` : String(label)
  const extras = payload.filter(p => p.name !== 'Produção' && p.name !== 'Sem comunicação')
  return (
    <div className="bg-superficie border border-borda px-3 py-2 text-xs shadow-md">
      <p className="text-texto-2 mb-1">{titulo}</p>
      <p style={{ color: COR_PRODUCAO }}>Produção: {producao.toLocaleString('pt-BR')}</p>
      {semComunicacao > 0 && (
        <>
          <p className="text-texto-3">Sem comunicação: {semComunicacao.toLocaleString('pt-BR')}</p>
          <p className="text-texto font-medium">Produção bruta: {(producao + semComunicacao).toLocaleString('pt-BR')}</p>
        </>
      )}
      {extras.map(e => (
        <p key={e.name} style={{ color: e.color }}>{e.name}: {e.value}</p>
      ))}
    </div>
  )
}

type TipoGrafico = 'producao' | 'paradasHora' | 'paradasMotivo'

const OPCOES_GRAFICO: { valor: TipoGrafico; rotulo: string }[] = [
  { valor: 'producao', rotulo: 'Produção' },
  { valor: 'paradasHora', rotulo: 'Paradas por hora' },
  { valor: 'paradasMotivo', rotulo: 'Paradas por motivo' },
]

// Cores dos tipos de parada nos gráficos (por hora e por motivo): interna vermelho, externa amarelo, planejada roxo.
// Cor de cada tipo de evento na linha do tempo (sem comunicação em cinza, fora do OEE).
const COR_EVENTO = {
  Inicio: { ponto: 'bg-accent-600', texto: 'text-texto' },
  Fim: { ponto: 'bg-accent-600', texto: 'text-texto' },
  Marcha: { ponto: 'bg-green-600', texto: 'text-green-700 dark:text-green-400' },
  Parada: { ponto: 'bg-red-500', texto: 'text-red-700 dark:text-red-400' },
  SemComunicacao: { ponto: 'bg-texto-3', texto: 'text-texto-2' },
} as const

const CORES_TIPO_PARADA = { Interna: '#e60000', Externa: '#f5b800', Planejada: '#8b5cf6' } as const

function minutos(ms: number) {
  return Math.round(ms / 6000) / 10 // 1 casa decimal
}

// As métricas vêm do que está gravado, e a coleta IoT grava a produção a cada
// 5 min, nos múltiplos do relógio (10:00, 10:05...). O modal aberto recarrega logo depois de
// cada gravação (10 s de folga), sem piscar.
const INTERVALO_GRAVACAO_MS = 5 * 60 * 1000
const FOLGA_MS = 10 * 1000

function msAteProximaAtualizacao(agora = Date.now()) {
  const proxima = Math.ceil((agora - FOLGA_MS) / INTERVALO_GRAVACAO_MS) * INTERVALO_GRAVACAO_MS + FOLGA_MS
  return Math.max(1000, proxima - agora)
}

export default function MaquinaDetalheModal({ open, maquinaLinhaId, sessaoId = null, onFechar, onColetaFinalizada }: Props) {
  const [dados, setDados] = useState<SessaoDetalheDto | null>(null)
  const [loading, setLoading] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [camposSelecionados, setCamposSelecionados] = useState<Set<string>>(new Set())
  const [grafico, setGrafico] = useState<TipoGrafico>('producao')
  // Gráficos ou linha do tempo (um de cada vez, para a linha do tempo ter altura); na URL para o F5.
  const [painelUrl, setPainelUrl] = useParametroUrl('painel')
  const painel: 'graficos' | 'linha' = painelUrl === 'linha' ? 'linha' : 'graficos'

  // Editar o motivo de uma parada da linha do tempo (Manual ou Semi Automático) e ver o histórico
  const { usuario } = useAuth()
  const podeEditarMotivo = ['Administrador', 'Desenvolvedor', 'Auditor'].includes(usuario?.nivel ?? '')
  // Na URL (?editarParada=, ?historicoParada=) para o F5 reabrir junto com o detalhe.
  const [editandoParada, setEditandoParada] = useParametroUrl('editarParada')
  const [historicoParada, setHistoricoParada] = useParametroUrl('historicoParada')

  // Fechar o detalhe fecha também o que estava aberto dentro dele.
  function fechar() {
    setEditandoParada(null)
    setHistoricoParada(null)
    onFechar()
  }
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

    const alvo = `${maquinaLinhaId}:${sessaoId ?? ''}`
    async function carregar() {
      const primeira = carregadoPara.current !== alvo
      if (primeira) {
        setLoading(true)
        setErro(null)
        setDados(null)
      }
      try {
        const data = sessaoId
          ? await sessaoDetalheService.getSessaoDetalhe(sessaoId)
          : await sessaoDetalheService.getUltimaSessaoDetalhe(maquinaLinhaId!)
        if (!ativo) return
        setDados(data)
        setErro(null)
        if (primeira) setCamposSelecionados(new Set())
        carregadoPara.current = alvo
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
  }, [open, maquinaLinhaId, sessaoId, recarregar])

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
      const res = await fetchAutenticado(`/api/paradas/foto/${fotoPath}`)
      if (!res.ok) throw new Error('Erro ao carregar foto')
      const blob = await res.blob()
      const url = window.URL.createObjectURL(blob)
      window.open(url, '_blank')
    } catch {
      alert('Erro ao carregar a foto.')
    }
  }

  if (!open) return null

  // Velocidade nominal da sessão (garrafas/h): linha tracejada, marca vermelha no eixo e
  // percentual em cada barra, como no relatório
  const nominal = Number(dados?.velocidadeNominal) || 0

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
      if (producaoPonto?.semComunicacao) ponto['Sem comunicação'] = producaoPonto.semComunicacao
      if (producaoPonto && nominal > 0) ponto.percentual = Math.round((producaoPonto.quantidade / nominal) * 100)

      dados.camposExtras.forEach(campo => {
        const extraPonto = campo.pontos.find(p => p.hora === hora)
        if (extraPonto) ponto[campo.nome] = extraPonto.valor
      })

      return ponto
    })
  })()

  // Eixo da produção: vai até a nominal (com folga) e tem a nominal como marca, em vermelho
  const maiorProducao = Math.max(0, ...dadosGrafico.map(p => Number(p['Produção'] ?? 0) + Number(p['Sem comunicação'] ?? 0)))
  const tetoProducao = Math.ceil((Math.max(maiorProducao, nominal) * 1.12) / 100) * 100 || 100
  const passoProducao = Math.max(1, Math.ceil(tetoProducao / 4 / 50) * 50)
  const marcasBase = [0, 1, 2, 3, 4].map(i => Math.min(i * passoProducao, tetoProducao))
  const marcasProducao = nominal > 0
    ? [...marcasBase.filter(m => Math.abs(m - nominal) > tetoProducao * 0.07), nominal].sort((a, b) => a - b)
    : marcasBase

  return (
    <div className={modalOverlayDark}>
      <div className={`${modalPanel} w-[900px] h-[90vh]`}>

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
          <button onClick={fechar} className="text-texto-3 hover:text-texto">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
          </button>
        </div>

        {/* Coleta IoT em andamento (some quando a máquina não está em coleta).
            Numa sessão antiga escolhida pelo filtro por data, não faz sentido mostrar a coleta de agora. */}
        {maquinaLinhaId && (!sessaoId || dados?.status === 'EmAndamento') && (
          <PainelColetaIot
            maquinaLinhaId={maquinaLinhaId}
            onFinalizada={() => { onColetaFinalizada?.(); fechar() }}
          />
        )}

        {loading ? (
          <p className="text-xs text-texto-3 text-center py-8">Carregando...</p>
        ) : erro ? (
          <p className="text-xs text-red-400 text-center py-8">{erro}</p>
        ) : !dados ? (
          <p className="text-xs text-texto-3 text-center py-8">Nenhuma sessão registrada para esta máquina</p>
        ) : (
          <>
            {/* Métricas e o seletor Gráficos | Linha do tempo — fixos */}
            <div className="px-5 pt-4 flex-shrink-0">
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

              {/* O que ocupa o resto do modal: gráficos ou linha do tempo */}
              <div className="flex items-center justify-between gap-3 mb-3">
                {/* Botões de cada grupo com a mesma largura (a do rótulo mais comprido) */}
                <div className="grid grid-flow-col auto-cols-fr">
                  {([['graficos', 'Gráficos'], ['linha', 'Linha do tempo']] as const).map(([valor, rotulo]) => (
                    <button
                      key={valor}
                      onClick={() => setPainelUrl(valor === 'graficos' ? null : valor)}
                      className={`h-7 px-3 -ml-px first:ml-0 text-[11px] font-medium border transition-colors ${
                        painel === valor
                          ? 'relative bg-accent-600 text-accent-fg border-accent-600'
                          : 'border-borda-forte text-texto-2 hover:bg-superficie-2'
                      }`}
                    >
                      {rotulo}
                    </button>
                  ))}
                </div>
                {painel === 'graficos' && (
                  <div className="grid grid-flow-col auto-cols-fr">
                    {OPCOES_GRAFICO.map(o => (
                      <button
                        key={o.valor}
                        onClick={() => setGrafico(o.valor)}
                        className={`h-7 px-3 -ml-px first:ml-0 text-[11px] font-medium border transition-colors ${
                          grafico === o.valor
                            ? 'relative bg-accent-600 text-accent-fg border-accent-600'
                            : 'border-borda-forte text-texto-2 hover:bg-superficie-2'
                        }`}
                      >
                        {o.rotulo}
                      </button>
                    ))}
                  </div>
                )}
              </div>
            </div>

            {painel === 'graficos' && (
              <div className="px-5 pb-4 flex-1 min-h-0 flex flex-col">
                {grafico === 'producao' && (
                  <div className="flex flex-wrap gap-2 mb-3">
                    <span className="text-[10px] px-2 py-1 bg-accent-50 dark:bg-accent-950 text-accent-600 dark:text-accent-400">Produção</span>
                    {dados.camposExtras.map((campo, i) => (
                      <button
                        key={campo.campoMaquinaId}
                        onClick={() => toggleCampo(campo.campoMaquinaId)}
                        className={`text-[10px] px-2 py-1 border transition-colors ${
                          camposSelecionados.has(campo.campoMaquinaId)
                            ? 'text-white border-transparent'
                            : 'border-borda-forte text-texto-2 hover:bg-superficie-2'
                        }`}
                        style={camposSelecionados.has(campo.campoMaquinaId) ? { backgroundColor: CORES_LINHA[i % CORES_LINHA.length] } : {}}
                      >
                        {campo.nome}{campo.unidade ? ` (${campo.unidade})` : ''}
                      </button>
                    ))}
                  </div>
                )}

              {/* select-none e sem contorno de foco: clicar no gráfico (ou perto dele) não marca
                  área de seleção nem desenha a moldura do navegador */}
              <div className={`flex-1 min-h-[16rem] ${areaGrafico}`}>
                {grafico === 'producao' ? (
                  <ResponsiveContainer width="100%" height="100%">
                    <ComposedChart data={dadosGrafico} accessibilityLayer={false}>
                      <CartesianGrid strokeDasharray="3 3" stroke="#e4e4e7" />
                      <XAxis
                        dataKey="hora"
                        interval={0}
                        tick={({ x, y, payload, index }) => {
                          // Hora em andamento: horário na cor de destaque e negrito, como a barra tracejada
                          const atual = dadosGrafico[index]?.parcial === true
                          return (
                            <text
                              x={x} y={Number(y) + 12} textAnchor="middle" fontSize={10}
                              fill={atual ? 'var(--color-accent-600)' : '#71717a'}
                              fontWeight={atual ? 700 : 400}
                            >
                              {payload.value}
                            </text>
                          )
                        }}
                      />
                      <YAxis
                        yAxisId="left"
                        ticks={marcasProducao}
                        domain={[0, tetoProducao]}
                        tick={({ x, y, payload }) => {
                          const ehNominal = nominal > 0 && payload.value === nominal
                          return (
                            <text x={x} y={y} textAnchor="end" dominantBaseline="central" fontSize={10} fontWeight={ehNominal ? 700 : 400} fill={ehNominal ? COR_NOMINAL : '#71717a'}>
                              {Number(payload.value).toLocaleString('pt-BR')}
                            </text>
                          )
                        }}
                      />
                      <YAxis yAxisId="right" orientation="right" tick={{ fontSize: 10 }} />
                      <Tooltip content={<DicaProducao />} />
                      <Legend wrapperStyle={{ fontSize: 11 }} />
                      {nominal > 0 && <ReferenceLine yAxisId="left" y={nominal} stroke={COR_NOMINAL} strokeWidth={1.5} strokeDasharray="6 4" />}
                      <Bar yAxisId="left" dataKey="Produção" stackId="producao" fill={COR_PRODUCAO}>
                        {dadosGrafico.map((ponto, i) => (
                          <Cell
                            key={i}
                            fill={ponto.parcial ? 'color-mix(in srgb, #1961ac 30%, transparent)' : COR_PRODUCAO}
                            stroke={ponto.parcial ? 'var(--color-accent-600)' : 'none'}
                            strokeWidth={ponto.parcial ? 1.5 : 0}
                            strokeDasharray={ponto.parcial ? '4 3' : undefined}
                          />
                        ))}
                        <LabelList dataKey="Produção" content={RotuloProducao} />
                      </Bar>
                      {/* Produção feita sem comunicação: em cinza, em cima da normal (fora do OEE) */}
                      {dados.pontosProducao.some(p => (p.semComunicacao ?? 0) > 0) && (
                        <Bar yAxisId="left" dataKey="Sem comunicação" stackId="producao" fill="#a1a1aa" />
                      )}
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
                ) : grafico === 'paradasHora' ? (
                  <GraficoParadasPorHora horas={dados.paradasPorHora ?? []} />
                ) : (
                  <GraficoParadasPorMotivo motivos={dados.paradasPorMotivo ?? []} />
                )}
              </div>
              </div>
            )}

            {/* Linha do tempo — ocupa o resto do modal, com rolagem própria. O mais recente em cima:
                o que está acontecendo agora aparece primeiro, sem rolar. */}
            {painel === 'linha' && (
            <div className="flex-1 overflow-y-auto px-5 pb-4 min-h-0">
              <div className="flex flex-col">
                {[...dados.eventos].reverse().map((evento, i) => (
                  <div key={i} className="flex items-start gap-3 py-2 border-b border-borda-suave last:border-0">
                    {/* A bolinha fica numa caixa da altura da linha do título, centrada nela */}
                    <div className="h-4 flex items-center flex-shrink-0">
                      <div className={`w-2 h-2 rounded-full ${COR_EVENTO[evento.tipo].ponto}`} />
                    </div>
                    <div className="flex-1 flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <span className={`block text-xs leading-4 font-medium ${COR_EVENTO[evento.tipo].texto}`}>
                          {evento.tipo === 'SemComunicacao' ? 'Sem comunicação'
                            : evento.tipo === 'Inicio' ? 'Medição iniciada'
                            : evento.tipo === 'Fim' ? (evento.motivoNome === 'ViradaDoDia' ? 'Dia encerrado à meia-noite' : 'Medição finalizada')
                            : evento.tipo}
                        </span>
                        {evento.tipo === 'Parada' && (
                          <p className="text-[11px] text-texto-2 mt-0.5">
                            {evento.motivoNome ?? <span className="text-amber-600 dark:text-amber-400">sem motivo (conta como interna)</span>}
                          </p>
                        )}
                        {evento.tipo === 'SemComunicacao' && (
                          <p className="text-[11px] text-texto-2 mt-0.5">
                            Fora do OEE
                          </p>
                        )}
                        {evento.tipo === 'Parada' && evento.paradaId && (
                          <div className="flex items-center gap-2 mt-1 text-[10px]">
                            {podeEditarMotivo && dados.maquinaId && (
                              <>
                                <button onClick={() => setEditandoParada(evento.paradaId!)} className="text-accent-600 dark:text-accent-400 hover:underline">
                                  Editar motivo
                                </button>
                                <span className="text-texto-4">|</span>
                              </>
                            )}
                            <button onClick={() => setHistoricoParada(evento.paradaId!)} className="text-[10px] text-texto-3 hover:text-texto hover:underline">
                              Histórico
                            </button>
                          </div>
                        )}
                      </div>
                      {/* Data, início e fim; embaixo, quanto tempo ficou assim (em curso: até agora) */}
                      <HorarioEvento evento={evento} />
                      {evento.fotoPath && (
                        <button
                          onClick={() => abrirFoto(evento.fotoPath!)}
                          title="Ver foto da parada"
                          className="flex-shrink-0 text-texto-3 hover:text-accent-600 dark:hover:text-accent-400 transition-colors"
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
            )}
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

// À direita de cada evento: "01/10/2026 - 14:31:41 - 14:33:56" e, embaixo, a duração.
// Marcha só tem o instante em que começou.
function HorarioEvento({ evento }: { evento: EventoTimelineDto }) {
  const de = new Date(evento.horario)
  const data = de.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' })
  const temPeriodo = evento.tipo !== 'Marcha' && evento.duracaoMs !== null
  const emAndamento = evento.emAndamento === true
  const ate = temPeriodo ? new Date(de.getTime() + evento.duracaoMs!) : null

  return (
    <div className="flex-shrink-0 text-right text-[11px] text-texto-2 tabular-nums">
      <p>
        {data} - {formatarHorario(de)}
        {temPeriodo && (
          <> - {emAndamento ? <span className="text-amber-600 dark:text-amber-400">em andamento</span> : formatarHorario(ate!, de)}</>
        )}
      </p>
      {temPeriodo && (
        <p className="mt-0.5">
          {emAndamento ? 'Até agora' : 'Duração'}: <span className="font-medium text-texto-2">{formatarDuracao(evento.duracaoMs!)}</span>
        </p>
      )}
    </div>
  )
}

function MetricaCard({ label, valor, destaque }: { label: string; valor: string; destaque?: boolean }) {
  return (
    <div className={metricaBox}>
      <p className={`${metricaValor} ${destaque ? 'text-accent-600 dark:text-accent-400' : ''}`}>
        {valor}
      </p>
      <p className={metricaLabel}>{label}</p>
    </div>
  )
}

// Minutos parados em cada hora da sessão, empilhados por tipo (a hora 14:00 = das 14:00 às 14:59).
function GraficoParadasPorHora({ horas }: { horas: ParadaPorHoraDto[] }) {
  if (horas.length === 0) return <p className="text-xs text-texto-3 text-center pt-24">Nenhuma parada nesta sessão</p>

  const dadosHoras = horas.map(h => ({
    hora: formatarHora(h.hora),
    Interna: minutos(h.internaMs),
    Externa: minutos(h.externaMs),
    Planejada: minutos(h.planejadaMs),
  }))

  return (
    <ResponsiveContainer width="100%" height="100%">
      <BarChart data={dadosHoras} accessibilityLayer={false}>
        <CartesianGrid strokeDasharray="3 3" stroke="#e4e4e7" />
        <XAxis dataKey="hora" interval={0} tick={{ fontSize: 10 }} />
        <YAxis tick={{ fontSize: 10 }} unit=" min" width={50} domain={[0, (max: number) => Math.max(10, Math.ceil((max * 1.15) / 5) * 5)]} />
        <Tooltip formatter={valor => `${valor} min`} />
        <Legend wrapperStyle={{ fontSize: 11 }} />
        <Bar dataKey="Interna" stackId="paradas" fill={CORES_TIPO_PARADA.Interna} />
        <Bar dataKey="Externa" stackId="paradas" fill={CORES_TIPO_PARADA.Externa} />
        <Bar dataKey="Planejada" stackId="paradas" fill={CORES_TIPO_PARADA.Planejada} />
      </BarChart>
    </ResponsiveContainer>
  )
}

// Tempo parado por motivo, do maior para o menor (Pareto), na cor do tipo do motivo.
function GraficoParadasPorMotivo({ motivos }: { motivos: ParadaPorMotivoDto[] }) {
  if (motivos.length === 0) return <p className="text-xs text-texto-3 text-center pt-24">Nenhuma parada nesta sessão</p>

  const dadosMotivos = motivos.map(m => ({
    motivo: m.motivo,
    tipo: m.tipo,
    minutos: minutos(m.duracaoMs),
    quantidade: m.quantidade,
  }))

  return (
    <ResponsiveContainer width="100%" height="100%">
      <BarChart data={dadosMotivos} layout="vertical" accessibilityLayer={false} margin={{ left: 8, right: 24 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="#e4e4e7" horizontal={false} />
        <XAxis type="number" tick={{ fontSize: 10 }} unit=" min" />
        <YAxis type="category" dataKey="motivo" width={170} interval={0} tick={{ fontSize: 10 }} />
        <Tooltip
          formatter={(valor, _nome, item) => {
            const q = (item?.payload as { quantidade?: number } | undefined)?.quantidade ?? 0
            return [`${valor} min · ${q} ${q === 1 ? 'parada' : 'paradas'}`, 'Tempo parado']
          }}
        />
        <Bar dataKey="minutos" name="Tempo parado">
          {dadosMotivos.map((m, i) => (
            <Cell key={i} fill={CORES_TIPO_PARADA[m.tipo]} />
          ))}
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  )
}
