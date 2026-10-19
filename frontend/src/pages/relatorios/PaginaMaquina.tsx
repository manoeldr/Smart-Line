// Páginas 2 em diante do relatório — uma por máquina medida no dia, com os mesmos indicadores
// do detalhe da máquina (modal do Dashboard) e três gráficos: produção por hora em colunas,
// paradas por motivo em barras deitadas e paradas por hora em linhas (uma por tipo). A produção
// por hora é a bruta (normal + sem comunicação), numa barra só. Larguras fixas: a folha é A4 e, na impressão,
// o gráfico não pode depender de medir a janela.
import { BarChart, Bar, Cell, LabelList, LineChart, Line, ReferenceLine, XAxis, YAxis, CartesianGrid, Legend } from 'recharts'
import type { SessaoDetalheDto } from '../../services/sessaoDetalheService'
import { areaGrafico } from '../../styles/graficos'
import { inteiro } from './formatos'
import type { Bloco } from './blocos'
import type { ParteMaquina } from './partesMaquina'

// Cor pelo tipo da parada, igual ao detalhe da máquina: interna vermelho, externa amarelo, programada roxo
const CORES_TIPO: Record<string, string> = { Interna: '#e60000', Externa: '#f5b800', Planejada: '#8b5cf6' }
const CORES_MOTIVO = ['#ef4444', '#f59e0b', '#3b82f6', '#10b981', '#8b5cf6', '#ec4899', '#14b8a6', '#f97316', '#64748b', '#a3e635']
const COR_PRODUCAO = '#1961ac'
const COR_NOMINAL = '#ef4444'

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

// Marca do eixo de produção: a da velocidade nominal sai em vermelho e negrito, as outras em cinza
function MarcaEixoProducao(props: { x?: number | string; y?: number | string; payload?: { value?: number }; nominal: number }) {
  const valor = props.payload?.value ?? 0
  const ehNominal = props.nominal > 0 && valor === props.nominal
  return (
    <text x={Number(props.x)} y={Number(props.y)} textAnchor="end" dominantBaseline="central" fontSize={11} fontWeight={ehNominal ? 700 : 400} fill={ehNominal ? COR_NOMINAL : '#52525b'}>
      {inteiro(valor)}
    </text>
  )
}

// Produção da hora na vertical: dentro da barra, em branco, quando cabe; senão logo acima
// dela, em cinza-escuro.
function RotuloProducao(props: { x?: number | string; y?: number | string; width?: number | string; height?: number | string; value?: unknown }) {
  const x = Number(props.x), y = Number(props.y), w = Number(props.width), h = Number(props.height)
  const valor = Number(props.value)
  if (!Number.isFinite(valor) || valor <= 0 || !Number.isFinite(x) || !Number.isFinite(w)) return null
  const texto = inteiro(valor)
  const alturaTexto = texto.length * 7 + 8
  const cx = x + w / 2
  if (h >= alturaTexto) {
    return (
      <text x={cx} y={y + h / 2} textAnchor="middle" dominantBaseline="central" fontSize={11} fontWeight={600} fill="#ffffff" transform={`rotate(-90 ${cx} ${y + h / 2})`}>
        {texto}
      </text>
    )
  }
  const cy = y - 4 - alturaTexto / 2
  return (
    <text x={cx} y={cy} textAnchor="middle" dominantBaseline="central" fontSize={11} fontWeight={600} fill="#27272a" transform={`rotate(-90 ${cx} ${cy})`}>
      {texto}
    </text>
  )
}

function Quadro({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div className="min-w-0 border border-borda bg-superficie-2 px-2 py-1.5 text-center">
      <p className="text-[10pt] text-texto-3 leading-tight whitespace-nowrap overflow-hidden">{rotulo}</p>
      <p className="text-[10pt] leading-tight text-texto font-semibold mt-0.5">{valor}</p>
    </div>
  )
}

function Titulo({ children }: { children: React.ReactNode }) {
  return <h3 className="text-[10pt] font-semibold uppercase tracking-wider text-texto-2 mb-2">{children}</h3>
}

interface Props {
  numero: number
  sessao: SessaoDetalheDto
  // Blocos ligados pelo usuário (valem para todas as máquinas); a linha do tempo vai em folhas próprias
  blocos: Bloco[]
  // Não cabe tudo numa folha: parte "indicadores" = quadros + produção por hora + paradas por
  // motivo; parte "paradas" = paradas por hora. O título da máquina só na primeira folha.
  parte: ParteMaquina
  comTitulo: boolean
}

export default function PaginaMaquina({ numero, sessao, blocos, parte, comTitulo }: Props) {
  const tem = (b: Bloco) => blocos.includes(b) && (
    parte === 'indicadores' ? b !== 'paradasHora' : b === 'paradasHora')
  // Velocidade nominal (garrafas/h) da sessão: linha tracejada vermelha e base do percentual
  const nominal = Number(sessao.velocidadeNominal) || 0
  // Uma barra por hora com a produção bruta (normal + sem comunicação), toda em azul
  const producao = sessao.pontosProducao.map(p => ({
    hora: horaCurta(p.hora),
    quantidade: p.quantidade + (p.semComunicacao ?? 0),
    parcial: !!p.parcial,
  }))
  const maiorProducao = Math.max(0, ...producao.map(p => p.quantidade))
  const tetoProducao = Math.ceil((Math.max(maiorProducao * 1.3, nominal * 1.12)) / 100) * 100
  // Marcas do eixo: 4 divisões iguais mais a nominal no lugar dela (em vermelho); uma marca
  // normal que ficaria colada na nominal sai, para não sobrepor
  const passo = Math.max(1, Math.ceil(tetoProducao / 4 / 50) * 50)
  const marcasBase = [0, 1, 2, 3, 4].map(i => Math.min(i * passo, tetoProducao))
  const marcasProducao = nominal > 0
    ? [...marcasBase.filter(m => Math.abs(m - nominal) > tetoProducao * 0.07), nominal].sort((a, b) => a - b)
    : marcasBase

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
      {comTitulo && <h2 className="flex items-baseline justify-between gap-4 text-[14pt] font-semibold uppercase tracking-wider text-texto-2 border-b-2 border-texto pb-1 mb-3">
        <span>
          {numero}. {sessao.maquinaNome}
        </span>
        <span className="normal-case tracking-normal font-normal text-[10pt] text-texto-3 whitespace-nowrap">
          {horaCurta(sessao.inicio)} – {sessao.fim ? horaCurta(sessao.fim) : 'em andamento'}
        </span>
      </h2>}

      {/* Indicadores — os mesmos do detalhe da máquina */}
      {tem('indicadores') && <div className="grid grid-cols-4 gap-2">
        <Quadro rotulo="OEE" valor={pct(sessao.oee)} />
        <Quadro rotulo="Eficiência" valor={pct(sessao.eficiencia)} />
        <Quadro rotulo="Disponibilidade" valor={pct(sessao.disponibilidade)} />
        <Quadro rotulo="Qualidade" valor={pct(sessao.qualidade)} />
        <Quadro rotulo="Tempo rodando" valor={horasMin(sessao.tempoRodandoMs)} />
        <Quadro rotulo="Tempo parado" valor={horasMin(sessao.tempoParadoMs)} />
        <Quadro rotulo="Produção total" valor={inteiro(sessao.producao)} />
        <Quadro rotulo="Refugo total" valor={inteiro(sessao.refugo)} />
        <Quadro rotulo="MTTR" valor={horasMin(sessao.mttrMs)} />
        <Quadro rotulo="MTBF" valor={horasMin(sessao.mtbfMs)} />
        <Quadro rotulo="Paradas internas" valor={String(sessao.numParadasInternas ?? 0)} />
        <Quadro rotulo="Paradas externas" valor={String(sessao.numParadasExternas ?? 0)} />
      </div>}

      {/* Produção por hora */}
      {tem('producaoHora') && <div className={`mt-4 ${areaGrafico}`}>
        <Titulo>Produção por hora</Titulo>
        {producao.length === 0 ? (
          <p className="text-[12pt] text-texto-3 italic">Sem leituras de produção.</p>
        ) : (
          <BarChart width={LARGURA} height={230} data={producao} margin={{ top: 8, right: 24, left: 0, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#e4e4e7" vertical={false} />
            <XAxis dataKey="hora" tick={{ fontSize: 11, fill: '#52525b' }} tickLine={false} axisLine={{ stroke: '#d4d4d8' }} interval={0} />
            <YAxis
              ticks={marcasProducao}
              tick={props => <MarcaEixoProducao {...props} nominal={nominal} />}
              tickLine={false}
              axisLine={false}
              width={56}
              domain={[0, tetoProducao]}
            />
            {nominal > 0 && <ReferenceLine y={nominal} stroke={COR_NOMINAL} strokeWidth={1.5} strokeDasharray="6 4" />}
            <Bar dataKey="quantidade" name="Produção" isAnimationActive={false}>
              {producao.map((p, i) => <Cell key={i} fill={COR_PRODUCAO} fillOpacity={p.parcial ? 0.5 : 1} />)}
              <LabelList dataKey="quantidade" content={RotuloProducao} />
            </Bar>
          </BarChart>
        )}
      </div>}

      {/* Paradas por motivo: uma linha por motivo, barra deitada, maior primeiro */}
      {tem('paradasMotivo') && <div className={`mt-4 ${areaGrafico}`}>
        <Titulo>Paradas por motivo</Titulo>
        {porMotivo.length === 0 ? (
          <p className="text-[12pt] text-texto-3 italic">Sem paradas.</p>
        ) : (
          <BarChart
            layout="vertical"
            width={LARGURA}
            height={30 + porMotivo.length * 24}
            data={porMotivo}
            margin={{ top: 4, right: 72, left: 8, bottom: 0 }}
            barCategoryGap={4}
          >
            <CartesianGrid strokeDasharray="3 3" stroke="#e4e4e7" horizontal={false} />
            <XAxis type="number" tick={{ fontSize: 11, fill: '#52525b' }} tickLine={false} axisLine={false} unit=" min" />
            <YAxis type="category" dataKey="rotulo" width={200} tick={{ fontSize: 11, fill: '#18181b' }} tickLine={false} axisLine={false} interval={0} />
            <Bar dataKey="minutos" name="Tempo parado" isAnimationActive={false} label={{ position: 'right', fontSize: 11, fill: '#52525b', formatter: (v: unknown) => `${v} min` }}>
              {porMotivo.map((m, i) => <Cell key={i} fill={CORES_TIPO[m.tipo] ?? CORES_MOTIVO[0]} />)}
            </Bar>
          </BarChart>
        )}
        {porMotivo.length > 0 && (
          <p className="mt-1 text-[8pt] text-texto-3 text-right">
            <span style={{ color: CORES_TIPO.Interna }}>■</span> interna{' '}
            <span style={{ color: CORES_TIPO.Externa }}>■</span> externa{' '}
            <span style={{ color: CORES_TIPO.Planejada }}>■</span> programada
          </p>
        )}
      </div>}

      {/* Paradas por hora: uma linha por tipo, como na visão Linha do Dashboard */}
      {tem('paradasHora') && <div className={`mt-4 ${areaGrafico}`}>
        <Titulo>Paradas por hora</Titulo>
        {porHora.length === 0 ? (
          <p className="text-[12pt] text-texto-3 italic">Sem paradas.</p>
        ) : (
          <LineChart width={LARGURA} height={230} data={porHora} margin={{ top: 8, right: 28, left: 0, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#e4e4e7" vertical={false} />
            <XAxis dataKey="nome" tick={{ fontSize: 11, fill: '#52525b' }} tickLine={false} axisLine={{ stroke: '#d4d4d8' }} interval={0} />
            <YAxis tick={{ fontSize: 11, fill: '#52525b' }} tickLine={false} axisLine={false} width={56} unit=" min" allowDecimals={false} domain={[0, (max: number) => Math.max(10, Math.ceil((max * 1.15) / 5) * 5)]} />
            <Legend iconSize={8} align="right" wrapperStyle={{ fontSize: 11 }} />
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
      </div>}
    </>
  )
}
