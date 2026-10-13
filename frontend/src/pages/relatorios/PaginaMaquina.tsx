// Páginas 2 em diante do relatório — uma por máquina medida no dia, com os mesmos indicadores
// do detalhe da máquina (modal do Dashboard) e três gráficos: produção por hora em colunas,
// paradas por motivo em barras deitadas e paradas por hora em linhas (uma por tipo). Larguras fixas: a folha é A4 e, na impressão,
// o gráfico não pode depender de medir a janela.
import { BarChart, Bar, Cell, LineChart, Line, XAxis, YAxis, CartesianGrid, Legend } from 'recharts'
import type { SessaoDetalheDto } from '../../services/sessaoDetalheService'
import { areaGrafico } from '../../styles/graficos'
import { inteiro } from './formatos'

// Cor pelo tipo da parada no relatório: interna vermelho, externa azul claro, programada cinza
const CORES_TIPO: Record<string, string> = { Interna: '#ef4444', Externa: '#1961ac', Planejada: '#a1a1aa' }
const CORES_MOTIVO = ['#ef4444', '#f59e0b', '#3b82f6', '#10b981', '#8b5cf6', '#ec4899', '#14b8a6', '#f97316', '#64748b', '#a3e635']
const COR_PRODUCAO = '#18181b'
const COR_SEM_COMUNICACAO = '#a1a1aa'

// Largura útil da folha (210 mm menos as margens) em px, com folga
const LARGURA = 640

function horasMin(ms: number | null | undefined): string {
  if (!Number.isFinite(ms)) return '—'
  const min = Math.max(0, Math.round((ms as number) / 60000))
  return `${Math.floor(min / 60)}h ${min % 60}m`
}

function pct(v: number | null | undefined): string {
  return Number.isFinite(v) ? `${(v as number).toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%` : '—'
}

function horaCurta(iso: string): string {
  return new Date(iso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
}

function minutos(ms: number): number {
  return Math.round(ms / 60000)
}

function Quadro({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div className="flex-1 min-w-0 border border-borda bg-superficie-2 px-2 py-1.5 text-center">
      <p className="text-[7.5px] uppercase tracking-wide text-texto-3 leading-tight whitespace-nowrap overflow-hidden">{rotulo}</p>
      <p className="text-sm leading-tight text-texto font-semibold mt-0.5">{valor}</p>
    </div>
  )
}

function Titulo({ children }: { children: React.ReactNode }) {
  return <h3 className="text-[9px] font-semibold uppercase tracking-widest text-texto-2 mb-1">{children}</h3>
}

interface Props {
  numero: number
  sessao: SessaoDetalheDto
  critica: boolean
}

export default function PaginaMaquina({ numero, sessao, critica }: Props) {
  const producao = sessao.pontosProducao.map(p => ({
    hora: horaCurta(p.hora),
    quantidade: p.quantidade,
    semComunicacao: p.semComunicacao ?? 0,
    parcial: !!p.parcial,
  }))

  const porMotivo = (sessao.paradasPorMotivo ?? [])
    .filter(m => m.duracaoMs > 0)
    .map(m => ({
      nome: m.motivo,
      tipo: m.tipo,
      minutos: minutos(m.duracaoMs),
      quantidade: m.quantidade,
      rotulo: `${m.motivo} (${m.quantidade}×)`,
    }))

  // Todas as horas da sessão (inclusive sem parada: a linha vai a zero)
  const porHora = (sessao.paradasPorHora ?? []).map(h => ({
    nome: horaCurta(h.hora),
    Interna: minutos(h.internaMs),
    Externa: minutos(h.externaMs),
    Planejada: minutos(h.planejadaMs),
  }))

  return (
    <>
      <h2 className="text-xs font-semibold uppercase tracking-widest text-texto-2 border-b-2 border-texto pb-1 mb-2">
        {numero}. {sessao.maquinaNome}
        {critica && <span className="normal-case tracking-normal font-normal text-texto-3"> · máquina crítica</span>}
        <span className="float-right normal-case tracking-normal font-normal text-texto-3">
          {horaCurta(sessao.inicio)} – {sessao.fim ? horaCurta(sessao.fim) : 'em andamento'}
        </span>
      </h2>

      {/* Indicadores — os mesmos do detalhe da máquina */}
      <div className="flex flex-col gap-2">
        <div className="flex gap-2">
          <Quadro rotulo="OEE" valor={pct(sessao.oee)} />
          <Quadro rotulo="Eficiência" valor={pct(sessao.eficiencia)} />
          <Quadro rotulo="Disponibilidade" valor={pct(sessao.disponibilidade)} />
          <Quadro rotulo="Qualidade" valor={pct(sessao.qualidade)} />
          <Quadro rotulo="Tempo rodando" valor={horasMin(sessao.tempoRodandoMs)} />
        </div>
        <div className="flex gap-2">
          <Quadro rotulo="Tempo parado" valor={horasMin(sessao.tempoParadoMs)} />
          <Quadro rotulo="Produção total" valor={inteiro(sessao.producao)} />
          <Quadro rotulo="Refugo total" valor={inteiro(sessao.refugo)} />
          <Quadro rotulo="MTTR" valor={horasMin(sessao.mttrMs)} />
          <Quadro rotulo="MTBF" valor={horasMin(sessao.mtbfMs)} />
        </div>
      </div>

      {/* Produção por hora */}
      <div className={`mt-4 ${areaGrafico}`}>
        <Titulo>Produção por hora</Titulo>
        {producao.length === 0 ? (
          <p className="text-[10px] text-texto-3 italic">Sem leituras de produção.</p>
        ) : (
          <BarChart width={LARGURA} height={190} data={producao} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#e4e4e7" vertical={false} />
            <XAxis dataKey="hora" tick={{ fontSize: 8, fill: '#52525b' }} tickLine={false} axisLine={{ stroke: '#d4d4d8' }} interval={0} />
            <YAxis tick={{ fontSize: 8, fill: '#52525b' }} tickLine={false} axisLine={false} width={44} tickFormatter={v => inteiro(v)} />
            <Bar dataKey="quantidade" name="Produção" stackId="p" isAnimationActive={false}>
              {producao.map((p, i) => <Cell key={i} fill={COR_PRODUCAO} fillOpacity={p.parcial ? 0.5 : 1} />)}
            </Bar>
            {producao.some(p => p.semComunicacao > 0) && (
              <Bar dataKey="semComunicacao" name="Sem comunicação" stackId="p" fill={COR_SEM_COMUNICACAO} isAnimationActive={false} />
            )}
          </BarChart>
        )}
      </div>

      {/* Paradas por motivo: uma linha por motivo, barra deitada, maior primeiro */}
      <div className={`mt-4 ${areaGrafico}`}>
        <Titulo>Paradas por motivo</Titulo>
        {porMotivo.length === 0 ? (
          <p className="text-[10px] text-texto-3 italic">Sem paradas.</p>
        ) : (
          <BarChart
            layout="vertical"
            width={LARGURA}
            height={28 + porMotivo.length * 18}
            data={porMotivo}
            margin={{ top: 4, right: 40, left: 8, bottom: 0 }}
            barCategoryGap={4}
          >
            <CartesianGrid strokeDasharray="3 3" stroke="#e4e4e7" horizontal={false} />
            <XAxis type="number" tick={{ fontSize: 8, fill: '#52525b' }} tickLine={false} axisLine={false} unit=" min" />
            <YAxis type="category" dataKey="rotulo" width={170} tick={{ fontSize: 8, fill: '#18181b' }} tickLine={false} axisLine={false} interval={0} />
            <Bar dataKey="minutos" name="Tempo parado" isAnimationActive={false} label={{ position: 'right', fontSize: 8, fill: '#52525b', formatter: (v: unknown) => `${v} min` }}>
              {porMotivo.map((m, i) => <Cell key={i} fill={CORES_TIPO[m.tipo] ?? CORES_MOTIVO[0]} />)}
            </Bar>
          </BarChart>
        )}
        {porMotivo.length > 0 && (
          <p className="mt-0.5 text-[8px] text-texto-3">
            <span style={{ color: CORES_TIPO.Interna }}>■</span> interna{' '}
            <span style={{ color: CORES_TIPO.Externa }}>■</span> externa{' '}
            <span style={{ color: CORES_TIPO.Planejada }}>■</span> programada
          </p>
        )}
      </div>

      {/* Paradas por hora: uma linha por tipo, como na visão Linha do Dashboard */}
      <div className={`mt-4 ${areaGrafico}`}>
        <Titulo>Paradas por hora</Titulo>
        {porHora.length === 0 ? (
          <p className="text-[10px] text-texto-3 italic">Sem paradas.</p>
        ) : (
          <LineChart width={LARGURA} height={190} data={porHora} margin={{ top: 8, right: 12, left: 0, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#e4e4e7" vertical={false} />
            <XAxis dataKey="nome" tick={{ fontSize: 8, fill: '#52525b' }} tickLine={false} axisLine={{ stroke: '#d4d4d8' }} interval={0} />
            <YAxis tick={{ fontSize: 8, fill: '#52525b' }} tickLine={false} axisLine={false} width={44} unit=" min" allowDecimals={false} />
            <Legend iconSize={8} wrapperStyle={{ fontSize: 9 }} />
            {(['Interna', 'Externa', 'Planejada'] as const)
              .filter(t => porHora.some(h => h[t] > 0))
              .map(t => (
                <Line
                  key={t}
                  dataKey={t}
                  name={t === 'Planejada' ? 'Programada' : t}
                  type="monotone"
                  stroke={CORES_TIPO[t]}
                  strokeWidth={2}
                  dot={{ r: 3, strokeWidth: 2, fill: '#ffffff' }}
                  isAnimationActive={false}
                />
              ))}
          </LineChart>
        )}
      </div>
    </>
  )
}
