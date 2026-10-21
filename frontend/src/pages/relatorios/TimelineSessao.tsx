// Relatório: linha do tempo em barra de uma máquina (a mesma do Overview), na escala de horas
// da sessão — verde produzindo, paradas por categoria e sem comunicação por cima, legenda embaixo.
import type { SessaoDetalheDto } from '../../services/sessaoDetalheService'
import { COR_TIMELINE as COR, montarTimeline, escalaHoras, horaCurta } from '../../utils/timelineParadas'

export default function TimelineSessao({ sessao, agora }: { sessao: SessaoDetalheDto; agora: Date }) {
  const t = montarTimeline(sessao, agora.getTime())
  const { inicioEscala, fimEscala, horas } = escalaHoras([t])
  const largura = Math.max(1, fimEscala - inicioEscala)
  const x = (v: number) => ((Math.min(Math.max(v, inicioEscala), fimEscala) - inicioEscala) / largura) * 100
  const passoMarca = horas.length > 14 ? 2 : 1

  return (
    <div className="mt-4">
      <h3 className="text-[10pt] font-semibold uppercase tracking-wider text-texto-2 mb-2">Linha do tempo</h3>
      <div className="relative h-7 bg-superficie-2 border border-borda">
        <div className="absolute top-0 h-full" style={{ left: `${x(t.inicio)}%`, width: `${x(t.fim) - x(t.inicio)}%`, backgroundColor: COR.rodando }} />
        {t.trechos.map((tr, i) => (
          <div key={i} className="absolute top-0 h-full" style={{ left: `${x(tr.inicio)}%`, width: `${Math.max(0.15, x(tr.fim) - x(tr.inicio))}%`, backgroundColor: COR[tr.tipo] }} />
        ))}
      </div>
      <div className="relative h-4 mt-0.5">
        {horas.map((h, i) => (
          i % passoMarca === 0 && (
            <span key={h} className="absolute -translate-x-1/2 text-[8pt] text-texto-3" style={{ left: `${x(h)}%` }}>{horaCurta(h)}</span>
          )
        ))}
      </div>
      <p className="mt-1 text-[8pt] text-texto-3 text-right">
        <span style={{ color: COR.rodando }}>■</span> produzindo{' '}
        <span style={{ color: COR.interna }}>■</span> interna{' '}
        <span style={{ color: COR.entrada }}>■</span> falta de produto na entrada{' '}
        <span style={{ color: COR.saida }}>■</span> saída acumulada
        {t.trechos.some(tr => tr.tipo === 'programada') && <>{' '}<span style={{ color: COR.programada }}>■</span> programada</>}
        {t.trechos.some(tr => tr.tipo === 'semComunicacao') && <>{' '}<span style={{ color: COR.semComunicacao }}>■</span> sem comunicação</>}
      </p>
    </div>
  )
}
