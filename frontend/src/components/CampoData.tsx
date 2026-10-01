// Campo de data com calendário próprio (no lugar do <input type="date"> do navegador, que não
// deixa marcar dias): mostra dd/mm/aaaa e, ao clicar, abre o mês com uma bolinha azul nos dias
// que tiveram sessão. Dias futuros e fora de [min, max] ficam desabilitados.
import { useEffect, useRef, useState } from 'react'
import { inputMd } from '../styles/inputs'

interface Props {
  // "yyyy-mm-dd"
  valor: string
  onChange: (valor: string) => void
  // Dias com sessão ("yyyy-mm-dd"): bolinha azul
  datasComSessao: string[]
  min?: string
  max?: string
}

const MESES = ['Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho', 'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro']
const DIAS_SEMANA = ['D', 'S', 'T', 'Q', 'Q', 'S', 'S']

function formatar(ano: number, mes: number, dia: number) {
  return `${ano}-${String(mes + 1).padStart(2, '0')}-${String(dia).padStart(2, '0')}`
}

function hojeTexto() {
  const h = new Date()
  return formatar(h.getFullYear(), h.getMonth(), h.getDate())
}

export default function CampoData({ valor, onChange, datasComSessao, min, max }: Props) {
  const [aberto, setAberto] = useState(false)
  const [ano, setAno] = useState(() => Number(valor.slice(0, 4)) || new Date().getFullYear())
  const [mes, setMes] = useState(() => (Number(valor.slice(5, 7)) || new Date().getMonth() + 1) - 1)
  const caixaRef = useRef<HTMLDivElement>(null)

  // Fecha ao clicar fora ou com Esc.
  useEffect(() => {
    if (!aberto) return
    function clique(e: MouseEvent) {
      if (caixaRef.current && !caixaRef.current.contains(e.target as Node)) setAberto(false)
    }
    function tecla(e: KeyboardEvent) {
      if (e.key === 'Escape') setAberto(false)
    }
    document.addEventListener('mousedown', clique)
    document.addEventListener('keydown', tecla)
    return () => {
      document.removeEventListener('mousedown', clique)
      document.removeEventListener('keydown', tecla)
    }
  }, [aberto])

  function abrir() {
    // Abre no mês da data escolhida.
    if (!aberto && valor) {
      setAno(Number(valor.slice(0, 4)))
      setMes(Number(valor.slice(5, 7)) - 1)
    }
    setAberto(a => !a)
  }

  function mudarMes(delta: number) {
    const d = new Date(ano, mes + delta, 1)
    setAno(d.getFullYear())
    setMes(d.getMonth())
  }

  const hoje = hojeTexto()
  const comSessao = new Set(datasComSessao)
  const primeiroDia = new Date(ano, mes, 1).getDay()
  const totalDias = new Date(ano, mes + 1, 0).getDate()
  const exibido = valor ? `${valor.slice(8, 10)}/${valor.slice(5, 7)}/${valor.slice(0, 4)}` : '—'

  return (
    <div ref={caixaRef} className="relative">
      <button type="button" onClick={abrir} className={`${inputMd} w-36 flex items-center justify-between gap-2 text-left`}>
        <span>{exibido}</span>
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="text-zinc-400">
          <rect x="3" y="4" width="18" height="18" rx="2" /><line x1="16" y1="2" x2="16" y2="6" /><line x1="8" y1="2" x2="8" y2="6" /><line x1="3" y1="10" x2="21" y2="10" />
        </svg>
      </button>

      {aberto && (
        <div className="absolute left-0 top-full mt-1 z-50 w-64 p-3 bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 rounded-xl shadow-lg">
          {/* Navegação do mês */}
          <div className="flex items-center justify-between mb-2">
            <span className="text-xs font-medium text-zinc-900 dark:text-zinc-100">{MESES[mes]} {ano}</span>
            <div className="flex gap-1">
              <button type="button" onClick={() => mudarMes(-1)} className="w-6 h-6 flex items-center justify-center rounded border border-zinc-200 dark:border-zinc-700 text-zinc-400 hover:bg-zinc-100 dark:hover:bg-zinc-800">
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="15 18 9 12 15 6" /></svg>
              </button>
              <button type="button" onClick={() => mudarMes(1)} className="w-6 h-6 flex items-center justify-center rounded border border-zinc-200 dark:border-zinc-700 text-zinc-400 hover:bg-zinc-100 dark:hover:bg-zinc-800">
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="9 18 15 12 9 6" /></svg>
              </button>
            </div>
          </div>

          {/* Dias */}
          <div className="grid grid-cols-7 gap-0.5">
            {DIAS_SEMANA.map((d, i) => (
              <div key={i} className="h-6 flex items-center justify-center text-[10px] font-medium text-zinc-400">{d}</div>
            ))}
            {Array.from({ length: primeiroDia }).map((_, i) => <div key={`v${i}`} />)}
            {Array.from({ length: totalDias }).map((_, i) => {
              const dia = i + 1
              const data = formatar(ano, mes, dia)
              const desabilitado = data > hoje || (min !== undefined && data < min) || (max !== undefined && data > max)
              const selecionado = data === valor
              const temSessao = comSessao.has(data)
              return (
                <button
                  key={dia}
                  type="button"
                  disabled={desabilitado}
                  onClick={() => { onChange(data); setAberto(false) }}
                  className={`h-7 w-full flex items-center justify-center text-[11px] rounded relative transition-colors
                    ${desabilitado ? 'text-zinc-300 dark:text-zinc-700 cursor-default' : 'cursor-pointer'}
                    ${selecionado ? 'bg-blue-600 text-white' : ''}
                    ${!selecionado && !desabilitado ? `${temSessao ? 'font-medium text-zinc-900 dark:text-zinc-100' : 'text-zinc-500'} hover:bg-zinc-100 dark:hover:bg-zinc-800` : ''}
                    ${data === hoje && !selecionado ? 'ring-1 ring-blue-600 ring-inset' : ''}
                  `}
                >
                  {dia}
                  {temSessao && (
                    <span className={`absolute bottom-0.5 left-1/2 -translate-x-1/2 w-1 h-1 rounded-full ${selecionado ? 'bg-white' : 'bg-blue-600'}`} />
                  )}
                </button>
              )
            })}
          </div>

          <p className="mt-2 flex items-center gap-1.5 text-[10px] text-zinc-400">
            <span className="w-1 h-1 rounded-full bg-blue-600" /> dia com sessão
          </p>
        </div>
      )}
    </div>
  )
}
