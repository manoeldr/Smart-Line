// Overview: linha do tempo das máquinas da linha, uma barra por máquina na escala de horas —
// verde onde produziu, por cima as paradas (interna, falta de produto na entrada, saída
// acumulada, outras externas, programada) e os trechos sem comunicação (ver utils/timelineParadas).
// Dados: o detalhe da sessão de cada máquina (em andamento, ou a do dia do filtro).
import { useEffect, useState } from 'react'
import { sessaoDetalheService } from '../../services/sessaoDetalheService'
import type { Linha } from '../../types'
import { cardPadded } from '../../styles/cards'
import { COR_TIMELINE as COR, montarTimeline, escalaHoras, horaCurta, type TimelineSessao } from '../../utils/timelineParadas'

interface LinhaMaquina extends TimelineSessao {
  nome: string
}

interface Props {
  linha: Linha
  // Muda a cada atualização do Overview (30 s): recarrega junto
  versao: number
}

export default function TimelineMaquinas({ linha, versao }: Props) {
  const [maquinas, setMaquinas] = useState<LinhaMaquina[] | null>(null)

  useEffect(() => {
    let ativo = true
    const agora = Date.now()
    const comSessao = [...linha.maquinas]
      .filter(m => m.ativo && (m.sessaoDia?.sessaoId || m.sessaoAtivaId))
      .sort((a, b) => a.ordem - b.ordem)
    Promise.all(comSessao.map(async m => {
      const sessaoId = m.sessaoDia?.sessaoId ?? m.sessaoAtivaId!
      try {
        return { nome: m.maquinaNome, ...montarTimeline(await sessaoDetalheService.getSessaoDetalhe(sessaoId), agora) }
      } catch {
        return null
      }
    })).then(lista => { if (ativo) setMaquinas(lista.filter((x): x is LinhaMaquina => x !== null)) })
    return () => { ativo = false }
  }, [linha, versao])

  const { inicioEscala, fimEscala, horas } = escalaHoras(maquinas ?? [])
  const largura = Math.max(1, fimEscala - inicioEscala)
  const x = (t: number) => ((Math.min(Math.max(t, inicioEscala), fimEscala) - inicioEscala) / largura) * 100
  // Com muitas horas, mostra uma marca sim, outra não, para não amontoar
  const passoMarca = horas.length > 14 ? 2 : 1

  return (
    <div className={cardPadded}>
      <div className="flex items-baseline justify-between mb-2">
        <p className="text-sm font-medium text-texto">Linha do tempo das máquinas</p>
        <p className="text-[10px] text-texto-3">
          <span style={{ color: COR.rodando }}>■</span> produzindo{' '}
          <span style={{ color: COR.interna }}>■</span> interna{' '}
          <span style={{ color: COR.entrada }}>■</span> falta de produto na entrada{' '}
          <span style={{ color: COR.saida }}>■</span> saída acumulada{' '}
          <span style={{ color: COR.semComunicacao }}>■</span> sem comunicação
        </p>
      </div>
      {maquinas === null ? (
        <div className="h-56 flex items-center justify-center text-xs text-texto-3">Carregando...</div>
      ) : maquinas.length === 0 ? (
        <div className="h-56 flex items-center justify-center text-xs text-texto-3">Nenhuma máquina em medição</div>
      ) : (
        <div className="flex flex-col gap-1.5">
          {maquinas.map(m => (
            <div key={m.nome} className="flex items-center gap-2">
              <span className="w-24 flex-shrink-0 text-[11px] text-texto truncate" title={m.nome}>{m.nome}</span>
              <div className="relative flex-1 h-5 bg-superficie-2">
                <div
                  className="absolute top-0 h-full"
                  style={{ left: `${x(m.inicio)}%`, width: `${x(m.fim) - x(m.inicio)}%`, backgroundColor: COR.rodando }}
                  title={`${m.nome} · ${horaCurta(m.inicio)}–${m.emAndamento ? 'em andamento' : horaCurta(m.fim)}`}
                />
                {m.trechos.map((t, i) => (
                  <div
                    key={i}
                    className="absolute top-0 h-full"
                    style={{ left: `${x(t.inicio)}%`, width: `${Math.max(0.15, x(t.fim) - x(t.inicio))}%`, backgroundColor: COR[t.tipo] }}
                    title={t.texto}
                  />
                ))}
              </div>
            </div>
          ))}
          <div className="flex items-center gap-2">
            <span className="w-24 flex-shrink-0" />
            <div className="relative flex-1 h-4">
              {horas.map((t, i) => (
                i % passoMarca === 0 && (
                  <span key={t} className="absolute -translate-x-1/2 text-[10px] text-texto-3" style={{ left: `${x(t)}%` }}>
                    {horaCurta(t)}
                  </span>
                )
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
