// Visão geral da linha no Dashboard: OEE da linha pela máquina crítica, as paradas somadas de
// todas as máquinas (com a máquina de onde veio cada uma) e o resumo de cada máquina.
// Cada máquina entra com a sessão em andamento, senão a última do período — a mesma dos cards.
import { useState } from 'react'
import {
  BarChart, Bar, Cell, LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer,
} from 'recharts'
import type { LinhaDashboardDto, MaquinaResumoLinhaDto, TempoParadoMaquinaDto } from '../../services/dashboardService'
import { badgeCritica } from '../../styles/badges'
import { cardPadded } from '../../styles/cards'
import { areaGrafico } from '../../styles/graficos'

interface Props {
  dados: LinhaDashboardDto
  onAbrirMaquina: (maquinaLinhaId: string) => void
}

type TipoGrafico = 'paradasMotivo' | 'paradasHora'

const OPCOES_GRAFICO: { valor: TipoGrafico; rotulo: string }[] = [
  { valor: 'paradasMotivo', rotulo: 'Paradas por motivo' },
  { valor: 'paradasHora', rotulo: 'Paradas por hora' },
]

const CHAVE_GRAFICO = 'smartline.dashboard.graficoParadas'
const CHAVE_OCULTAS = 'smartline.dashboard.maquinasOcultas'

function lerGuardado(chave: string): string | null {
  try { return localStorage.getItem(chave) } catch { return null }
}

function guardar(chave: string, valor: string) {
  try { localStorage.setItem(chave, valor) } catch { /* sem armazenamento: só não lembra */ }
}

// Uma cor por máquina, na ordem da linha (paleta categórica validada para daltonismo; a cor
// segue a máquina, não a posição no gráfico). A tabela abaixo repete os valores em texto.
const CORES_MAQUINA = ['#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#e87ba4', '#008300', '#4a3aa7', '#e34948']

function formatarHoras(ms: number) {
  const horas = Math.floor(ms / 3600000)
  const minutos = Math.floor((ms % 3600000) / 60000)
  return `${horas}h ${minutos}m`
}

function formatarHora(dataIso: string) {
  return new Date(dataIso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
}

function minutos(ms: number) {
  return Math.round(ms / 6000) / 10 // 1 casa decimal
}

function corOee(oee: number | null) {
  if (oee === null) return 'text-zinc-400'
  return oee >= 85
    ? 'text-green-600 dark:text-green-400'
    : oee >= 60
      ? 'text-amber-600 dark:text-amber-400'
      : 'text-red-600 dark:text-red-400'
}

function porcentagem(valor: number | null) {
  return valor === null ? '—' : `${valor}%`
}

// { [maquinaLinhaId]: minutos } para uma linha do gráfico empilhado.
function minutosPorMaquina(porMaquina: TempoParadoMaquinaDto[]) {
  return Object.fromEntries(porMaquina.map(p => [p.maquinaLinhaId, minutos(p.duracaoMs)]))
}

export default function LinhaGeral({ dados, onAbrirMaquina }: Props) {
  // Gráfico escolhido e máquinas escondidas (clicar no nome da legenda esconde/mostra) ficam
  // guardados neste navegador: o F5 volta com eles.
  const [grafico, setGraficoEstado] = useState<TipoGrafico>(() =>
    lerGuardado(CHAVE_GRAFICO) === 'paradasHora' ? 'paradasHora' : 'paradasMotivo')
  const [ocultas, setOcultas] = useState<Set<string>>(() => {
    try { return new Set<string>(JSON.parse(lerGuardado(CHAVE_OCULTAS) ?? '[]')) } catch { return new Set() }
  })

  function setGrafico(tipo: TipoGrafico) {
    setGraficoEstado(tipo)
    guardar(CHAVE_GRAFICO, tipo)
  }

  function alternarMaquina(maquinaLinhaId: string) {
    const novo = new Set(ocultas)
    if (novo.has(maquinaLinhaId)) novo.delete(maquinaLinhaId)
    else novo.add(maquinaLinhaId)
    setOcultas(novo)
    guardar(CHAVE_OCULTAS, JSON.stringify([...novo]))
  }

  const cores = Object.fromEntries(dados.maquinas.map((m, i) => [m.maquinaLinhaId, CORES_MAQUINA[i % CORES_MAQUINA.length]]))
  // Só as máquinas que pararam aparecem nos gráficos e na legenda.
  const maquinasComParada = dados.maquinas.filter(m => m.tempoParadoMs > 0)

  const referencia = dados.maquinaReferencia === null
    ? 'Nenhuma máquina com sessão no período'
    : dados.referenciaCritica
      ? `pela máquina crítica · ${dados.maquinaReferencia}`
      : `pela máquina de pior OEE · ${dados.maquinaReferencia} (nenhuma máquina crítica com sessão)`

  return (
    <div className="flex flex-col gap-3">

      {/* Indicadores da linha */}
      <div className={`${cardPadded} text-center`}>
        <div className="mb-4">
          <p className={`text-5xl font-medium ${corOee(dados.oee)}`}>{porcentagem(dados.oee)}</p>
          <p className="text-sm text-zinc-500 mt-1">OEE da linha</p>
          <p className="text-xs text-zinc-400">{referencia}</p>
        </div>

        <div className="grid grid-cols-3 gap-2 mb-4 max-w-xl mx-auto">
          <Indicador valor={porcentagem(dados.disponibilidade)} rotulo="Disponibilidade" />
          <Indicador valor={porcentagem(dados.performance)} rotulo="Eficiência" />
          <Indicador valor={porcentagem(dados.qualidade)} rotulo="Qualidade" />
        </div>

        <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 pt-3 border-t border-zinc-100 dark:border-zinc-800">
          <Indicador valor={dados.producao.toLocaleString('pt-BR')} rotulo="Produção da linha" />
          <Indicador valor={dados.refugoTotal.toLocaleString('pt-BR')} rotulo="Refugo (soma)" />
          <Indicador valor={formatarHoras(dados.tempoParadoTotalMs)} rotulo="Tempo parado (soma)" />
          <Indicador valor={dados.numParadas.toLocaleString('pt-BR')} rotulo="Paradas (soma)" />
        </div>
      </div>

      {/* Produção da máquina de referência, ao lado das paradas de todas as máquinas */}
      <div className="grid grid-cols-1 xl:grid-cols-2 gap-3">
      <div className={cardPadded}>
        <div className="flex items-center justify-between gap-3 mb-3 min-h-7">
          <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">
            Produção por hora{dados.maquinaReferencia && <span className="font-normal text-zinc-400"> · {dados.maquinaReferencia}</span>}
          </p>
        </div>
        <div className={`h-72 ${areaGrafico}`}>
          <GraficoProducao dados={dados} />
        </div>
      </div>

      {/* Gráficos das paradas somadas, empilhadas por máquina */}
      <div className={cardPadded}>
        <div className="flex items-center justify-between gap-3 mb-3">
          <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">Paradas de todas as máquinas</p>
          <div className="flex">
            {OPCOES_GRAFICO.map(o => (
              <button
                key={o.valor}
                onClick={() => setGrafico(o.valor)}
                className={`h-7 px-3 -ml-px first:ml-0 text-[11px] font-medium border transition-colors ${
                  grafico === o.valor
                    ? 'relative bg-blue-600 text-white border-blue-600'
                    : 'border-zinc-200 dark:border-zinc-700 text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800'
                }`}
              >
                {o.rotulo}
              </button>
            ))}
          </div>
        </div>
        <div className={`h-72 ${areaGrafico}`}>
          {grafico === 'paradasMotivo'
            ? <GraficoPorMotivo dados={dados} maquinas={maquinasComParada} cores={cores} ocultas={ocultas} onAlternar={alternarMaquina} />
            : <GraficoPorHora dados={dados} maquinas={maquinasComParada} cores={cores} ocultas={ocultas} onAlternar={alternarMaquina} />}
        </div>
      </div>
      </div>

      {/* Resumo de cada máquina — clicar abre o detalhe da máquina */}
      <div className={`${cardPadded} overflow-x-auto`}>
        <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100 mb-2">Máquinas da linha</p>
        <table className="w-full text-xs">
          <thead>
            <tr className="text-zinc-400 border-b border-zinc-100 dark:border-zinc-800">
              <th className="text-left font-normal py-1.5">Máquina</th>
              <th className="text-right font-normal py-1.5">OEE</th>
              <th className="text-right font-normal py-1.5">Produção</th>
              <th className="text-right font-normal py-1.5">Refugo</th>
              <th className="text-right font-normal py-1.5">Tempo parado</th>
              <th className="text-right font-normal py-1.5">Paradas</th>
            </tr>
          </thead>
          <tbody>
            {dados.maquinas.map(m => (
              <LinhaMaquina key={m.maquinaLinhaId} m={m} cor={cores[m.maquinaLinhaId]} onClick={() => onAbrirMaquina(m.maquinaLinhaId)} />
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

function LinhaMaquina({ m, cor, onClick }: { m: MaquinaResumoLinhaDto; cor: string; onClick: () => void }) {
  return (
    <tr
      onClick={onClick}
      className={`border-b border-zinc-50 dark:border-zinc-800/50 cursor-pointer hover:bg-zinc-50 dark:hover:bg-zinc-800/50 ${m.referencia ? 'font-medium' : ''}`}
    >
      <td className="py-2">
        <div className="flex items-center gap-2">
          <span className="inline-block w-2.5 h-2.5 flex-shrink-0" style={{ backgroundColor: cor }} />
          <span className="text-zinc-900 dark:text-zinc-100">{m.nome}</span>
          {m.critica && <span className={badgeCritica}>Crítica</span>}
          {m.aoVivo && <span className="text-[10px] text-green-600 dark:text-green-400">ao vivo</span>}
        </div>
      </td>
      {m.temSessao ? (
        <>
          <td className={`text-right ${corOee(m.oee)}`}>{porcentagem(m.oee)}</td>
          <td className="text-right text-zinc-700 dark:text-zinc-300">{m.producao.toLocaleString('pt-BR')}</td>
          <td className="text-right text-zinc-700 dark:text-zinc-300">{m.refugo.toLocaleString('pt-BR')}</td>
          <td className="text-right text-zinc-700 dark:text-zinc-300">{formatarHoras(m.tempoParadoMs)}</td>
          <td className="text-right text-zinc-700 dark:text-zinc-300">{m.numParadas}</td>
        </>
      ) : (
        <td colSpan={5} className="text-right text-zinc-400">sem sessão no período</td>
      )}
    </tr>
  )
}

// Produção por hora da máquina de referência, como no detalhe dela: cada barra no início da
// hora; a hora em andamento tracejada, mais clara e com o horário em destaque.
function GraficoProducao({ dados }: { dados: LinhaDashboardDto }) {
  if (dados.producaoPorHora.length === 0) {
    return <p className="text-xs text-zinc-400 text-center pt-24">Nenhuma produção registrada ainda</p>
  }

  const pontos = dados.producaoPorHora.map(p => ({
    hora: formatarHora(p.hora),
    fimDaHora: formatarHora(new Date(new Date(p.hora).getTime() + 3600000).toISOString()),
    parcial: p.parcial === true,
    producao: p.quantidade,
    // Sem valor (não 0) nas horas sem produção sem comunicação: a dica não mostra "Sem comunicação: 0".
    semComunicacao: p.semComunicacao ? p.semComunicacao : undefined,
  }))
  const temSemComunicacao = pontos.some(p => (p.semComunicacao ?? 0) > 0)

  return (
    <ResponsiveContainer width="100%" height="100%">
      <BarChart data={pontos} accessibilityLayer={false}>
        <CartesianGrid strokeDasharray="3 3" stroke="#e4e4e7" />
        <XAxis
          dataKey="hora"
          interval={0}
          tick={({ x, y, payload, index }) => {
            const atual = pontos[index]?.parcial === true
            return (
              <text
                x={x} y={Number(y) + 12} textAnchor="middle" fontSize={10}
                fill={atual ? '#1961c0' : '#71717a'}
                fontWeight={atual ? 700 : 400}
              >
                {payload.value}
              </text>
            )
          }}
        />
        <YAxis tick={{ fontSize: 10 }} width={50} />
        <Tooltip
          formatter={(valor, nome) => [Number(valor).toLocaleString('pt-BR'), nome]}
          labelFormatter={(rotulo, itens) => {
            const ponto = itens?.[0]?.payload as { parcial?: boolean; fimDaHora?: string } | undefined
            return ponto?.parcial
              ? `Em andamento: ${rotulo}–${ponto.fimDaHora} · parcial, atualiza a cada 5 min`
              : rotulo
          }}
        />
        <Bar dataKey="producao" name="Produção" stackId="producao" fill="#1961c0">
          {pontos.map((p, i) => (
            <Cell
              key={i}
              fill={p.parcial ? 'rgba(25, 97, 192, 0.3)' : '#1961c0'}
              stroke={p.parcial ? '#1961c0' : 'none'}
              strokeWidth={p.parcial ? 1.5 : 0}
              strokeDasharray={p.parcial ? '4 3' : undefined}
            />
          ))}
        </Bar>
        {/* Produção feita sem comunicação: em cinza, em cima da normal (fora do OEE) */}
        {temSemComunicacao && <Bar dataKey="semComunicacao" name="Sem comunicação" stackId="producao" fill="#a1a1aa" />}
      </BarChart>
    </ResponsiveContainer>
  )
}

interface GraficoProps {
  dados: LinhaDashboardDto
  maquinas: MaquinaResumoLinhaDto[]
  cores: Record<string, string>
  ocultas: Set<string>
  onAlternar: (maquinaLinhaId: string) => void
}

// Legenda clicável: o nome da máquina esconde/mostra a série dela (escondida fica em cinza).
function legendaClicavel(onAlternar: (maquinaLinhaId: string) => void) {
  return {
    wrapperStyle: { fontSize: 11, cursor: 'pointer', userSelect: 'none' as const },
    onClick: (item: { dataKey?: unknown }) => {
      if (typeof item.dataKey === 'string') onAlternar(item.dataKey)
    },
  }
}

const semParadas = <p className="text-xs text-zinc-400 text-center pt-24">Nenhuma parada nas máquinas da linha</p>

// Tempo parado por motivo (maior primeiro), uma barra por máquina lado a lado: o tamanho de
// cada barra é o tempo daquela máquina (empilhado dava a entender que a última parou o total).
function GraficoPorMotivo({ dados, maquinas, cores, ocultas, onAlternar }: GraficoProps) {
  if (dados.paradasPorMotivo.length === 0) return semParadas

  const linhas = dados.paradasPorMotivo.map(m => ({
    motivo: m.motivo,
    quantidade: m.quantidade,
    ...minutosPorMaquina(m.porMaquina),
  }))

  return (
    <ResponsiveContainer width="100%" height="100%">
      <BarChart data={linhas} layout="vertical" accessibilityLayer={false} margin={{ left: 8, right: 24 }} barGap={2}>
        <CartesianGrid strokeDasharray="3 3" stroke="#e4e4e7" horizontal={false} />
        <XAxis type="number" tick={{ fontSize: 10 }} unit=" min" />
        <YAxis type="category" dataKey="motivo" width={170} interval={0} tick={{ fontSize: 10 }} />
        <Tooltip formatter={valor => `${valor} min`} cursor={{ fill: 'rgba(161, 161, 170, 0.12)' }} />
        <Legend {...legendaClicavel(onAlternar)} />
        {maquinas.map(m => (
          <Bar key={m.maquinaLinhaId} dataKey={m.maquinaLinhaId} name={m.nome} fill={cores[m.maquinaLinhaId]} radius={[0, 4, 4, 0]} maxBarSize={14} hide={ocultas.has(m.maquinaLinhaId)} />
        ))}
      </BarChart>
    </ResponsiveContainer>
  )
}

// Minutos parados em cada hora (a hora 14:00 = das 14:00 às 14:59), uma linha por máquina.
function GraficoPorHora({ dados, maquinas, cores, ocultas, onAlternar }: GraficoProps) {
  if (dados.paradasPorHora.length === 0) return semParadas

  const linhas = dados.paradasPorHora.map(h => ({
    hora: formatarHora(h.hora),
    ...minutosPorMaquina(h.porMaquina),
  }))

  return (
    <ResponsiveContainer width="100%" height="100%">
      <LineChart data={linhas} accessibilityLayer={false} margin={{ right: 12 }}>
        <CartesianGrid strokeDasharray="3 3" stroke="#e4e4e7" />
        <XAxis dataKey="hora" interval={0} tick={{ fontSize: 10 }} />
        <YAxis tick={{ fontSize: 10 }} unit=" min" width={50} allowDecimals={false} />
        <Tooltip formatter={valor => `${valor} min`} />
        <Legend {...legendaClicavel(onAlternar)} />
        {maquinas.map(m => (
          <Line
            hide={ocultas.has(m.maquinaLinhaId)}
            key={m.maquinaLinhaId}
            dataKey={m.maquinaLinhaId}
            name={m.nome}
            type="monotone"
            stroke={cores[m.maquinaLinhaId]}
            strokeWidth={2}
            dot={{ r: 4, strokeWidth: 2, fill: '#ffffff' }}
            activeDot={{ r: 5 }}
          />
        ))}
      </LineChart>
    </ResponsiveContainer>
  )
}

// Valor em destaque com o rótulo embaixo, centralizados no espaço dele.
function Indicador({ valor, rotulo }: { valor: string; rotulo: string }) {
  return (
    <div className="flex flex-col items-center">
      <p className="text-lg font-medium text-zinc-900 dark:text-zinc-100">{valor}</p>
      <p className="text-xs text-zinc-400">{rotulo}</p>
    </div>
  )
}
