// Overview: indicadores da máquina crítica (a de referência da linha) em gauges — OEE,
// Disponibilidade, Performance, Qualidade — e os números de produção e refugo dela.
import type { LinhaDashboardDto } from '../../services/dashboardService'
import { cardPadded } from '../../styles/cards'

const COR_GAUGE = '#1961ac'

function Gauge({ rotulo, valor }: { rotulo: string; valor: number | null }) {
  // Meia-lua: arco de 180° de raio 40 num viewBox 100×60
  const raio = 40
  const comprimento = Math.PI * raio
  const fracao = valor == null ? 0 : Math.min(100, Math.max(0, valor)) / 100
  return (
    <div className="flex flex-col items-center">
      <svg viewBox="0 0 100 60" className="w-full max-w-[150px]">
        <path d="M10 50 A40 40 0 0 1 90 50" fill="none" stroke="var(--color-superficie-3)" strokeWidth="10" strokeLinecap="round" />
        <path
          d="M10 50 A40 40 0 0 1 90 50"
          fill="none"
          stroke={COR_GAUGE}
          strokeWidth="10"
          strokeLinecap="round"
          strokeDasharray={`${comprimento * fracao} ${comprimento}`}
        />
        <text x="50" y="50" textAnchor="middle" fontSize="16" fontWeight="600" fill="var(--color-texto)">
          {valor == null ? '—' : `${valor.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%`}
        </text>
      </svg>
      <span className="text-[11px] text-texto-2 -mt-1">{rotulo}</span>
    </div>
  )
}

function Numero({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div className="bg-superficie-2 px-3 py-2 text-center">
      <p className="text-base font-semibold text-texto">{valor}</p>
      <p className="text-[10px] text-texto-3">{rotulo}</p>
    </div>
  )
}

export default function GaugesCritica({ dados }: { dados: LinhaDashboardDto | null | undefined }) {
  const referencia = dados?.maquinas.find(m => m.referencia)
  return (
    <div className={cardPadded}>
      <div className="flex items-baseline justify-between mb-2">
        <p className="text-sm font-medium text-texto">
          Máquina crítica{referencia ? <span className="text-texto-2 font-normal"> · {referencia.nome}</span> : ''}
        </p>
        {dados && !dados.referenciaCritica && referencia && (
          <p className="text-[10px] text-texto-3">crítica sem medição: indicadores da {referencia.nome}</p>
        )}
      </div>
      {dados === undefined ? (
        <div className="h-32 flex items-center justify-center text-xs text-texto-3">Carregando...</div>
      ) : !dados || !referencia ? (
        <div className="h-32 flex items-center justify-center text-xs text-texto-3">Nenhuma máquina em medição</div>
      ) : (
        <div className="grid grid-cols-[repeat(4,minmax(0,1fr))_auto] gap-3 items-center">
          <Gauge rotulo="OEE" valor={dados.oee} />
          <Gauge rotulo="Disponibilidade" valor={dados.disponibilidade} />
          <Gauge rotulo="Performance" valor={dados.performance} />
          <Gauge rotulo="Qualidade" valor={dados.qualidade} />
          <div className="flex flex-col gap-2 min-w-28">
            <Numero rotulo="Produção" valor={dados.producao.toLocaleString('pt-BR')} />
            <Numero rotulo="Refugo" valor={referencia.refugo.toLocaleString('pt-BR')} />
          </div>
        </div>
      )}
    </div>
  )
}
