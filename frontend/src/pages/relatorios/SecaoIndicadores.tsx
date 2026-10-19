// Seção 3 do relatório — Indicadores gerais: disponibilidade, performance, qualidade e OEE da
// máquina crítica (a mesma sessão do Resumo geral; iguais ao detalhe dela no Dashboard).
import type { SessaoDetalheDto } from '../../services/sessaoDetalheService'

function pct(v: number | null | undefined): string {
  return Number.isFinite(v) ? `${(v as number).toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%` : '—'
}

function Quadro({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div className="min-w-0 border border-borda bg-superficie-2 px-2 py-2 text-center">
      <p className="text-[10pt] text-texto-3 leading-tight whitespace-nowrap overflow-hidden">{rotulo}</p>
      <p className="text-[10pt] leading-tight text-texto font-semibold mt-0.5">{valor}</p>
    </div>
  )
}

export default function SecaoIndicadores({ sessao }: { sessao: SessaoDetalheDto }) {
  return (
    <section className="break-inside-avoid mt-5">
      <h2 className="text-[14pt] font-semibold uppercase tracking-wider text-texto-2 border-b-2 border-texto pb-1 mb-3">
        Indicadores gerais
      </h2>
      <div className="grid grid-cols-4 gap-2">
        <Quadro rotulo="Disponibilidade" valor={pct(sessao.disponibilidade)} />
        <Quadro rotulo="Performance" valor={pct(sessao.eficiencia)} />
        <Quadro rotulo="Qualidade" valor={pct(sessao.qualidade)} />
        <Quadro rotulo="OEE" valor={pct(sessao.oee)} />
      </div>
    </section>
  )
}
