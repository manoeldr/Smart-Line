// Overview: linha do tempo das máquinas da linha, uma barra por máquina na escala de horas —
// verde onde produziu, por cima as paradas (interna, externa, programada) e os trechos sem
// comunicação. Dados: o detalhe da sessão de cada máquina (em andamento, ou a do dia do filtro).
import { useEffect, useState } from 'react'
import { sessaoDetalheService, type SessaoDetalheDto } from '../../services/sessaoDetalheService'
import type { Linha } from '../../types'
import { cardPadded } from '../../styles/cards'

const COR = {
  rodando: '#16a34a',
  Interna: '#e60000',
  Externa: '#f5b800',
  Planejada: '#8b5cf6',
  semComunicacao: '#d4d4d8',
} as const

interface Trecho {
  inicio: number
  fim: number
  tipo: 'Interna' | 'Externa' | 'Planejada' | 'semComunicacao'
  texto: string
}

interface LinhaMaquina {
  nome: string
  inicio: number
  fim: number
  emAndamento: boolean
  trechos: Trecho[]
}

function hora(ms: number) {
  return new Date(ms).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
}

function duracao(ms: number) {
  const min = Math.round(ms / 60000)
  return min < 60 ? `${min} min` : `${Math.floor(min / 60)} h ${String(min % 60).padStart(2, '0')} min`
}

function montar(nome: string, d: SessaoDetalheDto, agora: number): LinhaMaquina {
  const inicio = new Date(d.inicio).getTime()
  const fim = d.fim ? new Date(d.fim).getTime() : agora
  const trechos: Trecho[] = []
  for (const e of d.eventos) {
    if (e.tipo !== 'Parada' && e.tipo !== 'SemComunicacao') continue
    const ini = new Date(e.horario).getTime()
    const fimTrecho = e.duracaoMs != null ? ini + e.duracaoMs : fim
    if (e.tipo === 'SemComunicacao') {
      trechos.push({ inicio: ini, fim: fimTrecho, tipo: 'semComunicacao', texto: `Sem comunicação · ${hora(ini)}–${hora(fimTrecho)} · ${duracao(fimTrecho - ini)}` })
    } else {
      const tipo = e.motivoTipo === 'Externa' ? 'Externa' : e.motivoTipo === 'Planejada' ? 'Planejada' : 'Interna'
      const rotuloTipo = tipo === 'Planejada' ? 'programada' : tipo.toLowerCase()
      trechos.push({ inicio: ini, fim: fimTrecho, tipo, texto: `${e.motivoNome ?? 'Sem motivo'} (${rotuloTipo}) · ${hora(ini)}–${hora(fimTrecho)} · ${duracao(fimTrecho - ini)}` })
    }
  }
  return { nome, inicio, fim, emAndamento: !d.fim, trechos }
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
        return montar(m.maquinaNome, await sessaoDetalheService.getSessaoDetalhe(sessaoId), agora)
      } catch {
        return null
      }
    })).then(lista => { if (ativo) setMaquinas(lista.filter((x): x is LinhaMaquina => x !== null)) })
    return () => { ativo = false }
  }, [linha, versao])

  // Escala: da hora cheia antes do primeiro início até a hora cheia depois do último fim
  const inicioEscala = maquinas && maquinas.length > 0 ? Math.floor(Math.min(...maquinas.map(m => m.inicio)) / 3600000) * 3600000 : 0
  const fimEscala = maquinas && maquinas.length > 0 ? Math.ceil(Math.max(...maquinas.map(m => m.fim)) / 3600000) * 3600000 : 1
  const largura = Math.max(1, fimEscala - inicioEscala)
  const x = (t: number) => ((Math.min(Math.max(t, inicioEscala), fimEscala) - inicioEscala) / largura) * 100
  const horas: number[] = []
  for (let t = inicioEscala; t <= fimEscala; t += 3600000) horas.push(t)
  // Com muitas horas, mostra uma marca sim, outra não, para não amontoar
  const passoMarca = horas.length > 14 ? 2 : 1

  return (
    <div className={cardPadded}>
      <div className="flex items-baseline justify-between mb-2">
        <p className="text-sm font-medium text-texto">Linha do tempo das máquinas</p>
        <p className="text-[10px] text-texto-3">
          <span style={{ color: COR.rodando }}>■</span> produzindo{' '}
          <span style={{ color: COR.Interna }}>■</span> interna{' '}
          <span style={{ color: COR.Externa }}>■</span> externa{' '}
          <span style={{ color: COR.Planejada }}>■</span> programada{' '}
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
                {/* Produzindo: do início ao fim (ou agora) */}
                <div
                  className="absolute top-0 h-full"
                  style={{ left: `${x(m.inicio)}%`, width: `${x(m.fim) - x(m.inicio)}%`, backgroundColor: COR.rodando }}
                  title={`${m.nome} · ${hora(m.inicio)}–${m.emAndamento ? 'em andamento' : hora(m.fim)}`}
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
          {/* Eixo das horas */}
          <div className="flex items-center gap-2">
            <span className="w-24 flex-shrink-0" />
            <div className="relative flex-1 h-4">
              {horas.map((t, i) => (
                i % passoMarca === 0 && (
                  <span
                    key={t}
                    className="absolute -translate-x-1/2 text-[10px] text-texto-3"
                    style={{ left: `${x(t)}%` }}
                  >
                    {hora(t)}
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
