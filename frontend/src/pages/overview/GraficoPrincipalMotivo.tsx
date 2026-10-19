// Overview: o principal motivo de parada INTERNA de cada máquina da linha (o de maior tempo), em
// colunas, da máquina que mais parou para a que menos parou (o motivo aparece na dica). Os dados vêm da visão Linha do Dashboard
// (sessão em andamento de cada máquina, ou a do dia escolhido no filtro de data).
import { BarChart, Bar, Cell, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts'
import type { LinhaDashboardDto } from '../../services/dashboardService'
import { cardPadded } from '../../styles/cards'
import { areaGrafico } from '../../styles/graficos'

// Cores por tipo, iguais ao detalhe da máquina
const COR_TIPO: Record<string, string> = { Interna: '#e60000', Externa: '#f5b800', Planejada: '#8b5cf6' }

interface Coluna {
  maquina: string
  motivo: string
  tipo: string
  minutos: number
  quantidade: number
}

function principaisMotivos(linha: LinhaDashboardDto): Coluna[] {
  const porMaquina = new Map<string, Coluna>()
  for (const m of linha.paradasPorMotivo) {
    if (m.tipo !== 'Interna') continue
    for (const p of m.porMaquina) {
      const atual = porMaquina.get(p.maquinaLinhaId)
      if (!atual || p.duracaoMs > atual.minutos * 60000) {
        const nome = linha.maquinas.find(x => x.maquinaLinhaId === p.maquinaLinhaId)?.nome ?? '—'
        porMaquina.set(p.maquinaLinhaId, { maquina: nome, motivo: m.motivo, tipo: m.tipo, minutos: Math.round(p.duracaoMs / 60000), quantidade: p.quantidade ?? 0 })
      }
    }
  }
  return [...porMaquina.values()].filter(c => c.minutos > 0).sort((a, b) => b.minutos - a.minutos)
}

// dados: undefined = carregando; null = falhou (ver useLinhaGeral)
export default function GraficoPrincipalMotivo({ dados }: { dados: LinhaDashboardDto | null | undefined }) {
  const colunas = dados === undefined ? null : dados ? principaisMotivos(dados) : []

  return (
    <div className={`${cardPadded} ${areaGrafico}`}>
      <div className="flex items-baseline justify-between mb-2">
        <p className="text-sm font-medium text-texto">Principal motivo de parada interna por máquina</p>
      </div>
      {colunas === null ? (
        <div className="h-56 flex items-center justify-center text-xs text-texto-3">Carregando...</div>
      ) : colunas.length === 0 ? (
        <div className="h-56 flex items-center justify-center text-xs text-texto-3">Nenhuma parada interna nas máquinas da linha</div>
      ) : (
        <div className="h-64">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={colunas} accessibilityLayer={false} margin={{ top: 8, right: 12, left: 0, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#e4e4e7" vertical={false} />
              <XAxis dataKey="maquina" tick={{ fontSize: 10 }} interval={0} />
              <YAxis tick={{ fontSize: 10 }} unit=" min" width={56} allowDecimals={false} />
              <Tooltip
                cursor={{ fill: 'rgba(161, 161, 170, 0.12)' }}
                formatter={(valor, _nome, item) => {
                  const c = item?.payload as Coluna | undefined
                  return [`${valor} min · ${c?.quantidade ?? 0}×`, c ? `${c.motivo} (${c.tipo === 'Planejada' ? 'programada' : c.tipo.toLowerCase()})` : 'Parada']
                }}
              />
              <Bar dataKey="minutos" maxBarSize={56} isAnimationActive={false}>
                {colunas.map((c, i) => <Cell key={i} fill={COR_TIPO[c.tipo] ?? COR_TIPO.Interna} />)}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  )
}
