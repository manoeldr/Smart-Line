import { useState, useEffect, useRef } from 'react'

interface Props {
  open: boolean
  onClose: () => void
  onApply: (data: string) => void
  onClear: () => void
  // Data filtrada agora (nula = hoje, ao vivo)
  valor?: string | null
  datasComSessao: string[]
  anchorRef: React.RefObject<HTMLButtonElement | null>
}

const MESES = ['Janeiro','Fevereiro','Março','Abril','Maio','Junho','Julho','Agosto','Setembro','Outubro','Novembro','Dezembro']
const DIAS_SEMANA = ['D','S','T','Q','Q','S','S']

export default function DateFilterModal({ open, onClose, onApply, onClear, valor = null, datasComSessao, anchorRef }: Props) {
  const hoje = new Date()
  const hojeStr = `${hoje.getFullYear()}-${String(hoje.getMonth() + 1).padStart(2, '0')}-${String(hoje.getDate()).padStart(2, '0')}`
  // Abre no dia filtrado ou, sem filtro, em hoje (o modal é montado a cada abertura).
  const inicial = valor ?? hojeStr
  const [ano, setAno] = useState(parseInt(inicial.split('-')[0]))
  const [mes, setMes] = useState(parseInt(inicial.split('-')[1]) - 1)
  const [selecionado, setSelecionado] = useState<string | null>(inicial)
  const [pos, setPos] = useState({ top: 0, right: 0 })
  const popoverRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (open && anchorRef.current) {
      const rect = anchorRef.current.getBoundingClientRect()
      setPos({ top: rect.bottom + 10, right: window.innerWidth - rect.right })
    }
  }, [open, anchorRef])

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (
        popoverRef.current &&
        !popoverRef.current.contains(e.target as Node) &&
        anchorRef.current &&
        !anchorRef.current.contains(e.target as Node)
      ) onClose()
    }
    if (open) document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [open, onClose, anchorRef])

  if (!open) return null

  const primeiroDia = new Date(ano, mes, 1).getDay()
  const totalDias = new Date(ano, mes + 1, 0).getDate()

  function formatarData(d: number) {
    return `${ano}-${String(mes + 1).padStart(2, '0')}-${String(d).padStart(2, '0')}`
  }

  function isHoje(d: number) {
    return ano === hoje.getFullYear() && mes === hoje.getMonth() && d === hoje.getDate()
  }

  function isFuturo(d: number) {
    const data = new Date(ano, mes, d)
    const hojeZero = new Date(hoje.getFullYear(), hoje.getMonth(), hoje.getDate())
    return data > hojeZero
  }

  function temSessao(d: number) {
    return datasComSessao.includes(formatarData(d))
  }

  function handleDia(d: number) {
    if (isFuturo(d)) return
    setSelecionado(formatarData(d))
  }

  function mesAnterior() {
    if (mes === 0) { setMes(11); setAno(a => a - 1) }
    else setMes(m => m - 1)
    setSelecionado(null)
  }

  function proximoMes() {
    if (mes === 11) { setMes(0); setAno(a => a + 1) }
    else setMes(m => m + 1)
    setSelecionado(null)
  }

  function handleApply() {
    if (!selecionado) return
    // Hoje é o ao vivo: o mesmo que limpar o filtro.
    if (selecionado === hojeStr) { onClear(); onClose(); return }
    const d = parseInt(selecionado.split('-')[2])
    if (!temSessao(d)) return
    onApply(selecionado)
    onClose()
  }

  function handleClear() {
    setSelecionado(null)
    onClear()
    onClose()
  }

  const diaNumSel = selecionado ? parseInt(selecionado.split('-')[2]) : 0

  return (
    <div
      ref={popoverRef}
      style={{ position: 'fixed', top: pos.top, right: pos.right, zIndex: 50 }}
      className="w-68 bg-superficie border border-borda rounded-xl shadow-lg"
    >
      {/* Seta */}
      <div
        style={{ position: 'absolute', top: -7, right: 7 }}
        className="w-3 h-3 bg-superficie border-l border-t border-borda rotate-45"
      />

      <div className="p-4">
        {/* Header */}
        <div className="flex items-center justify-between mb-4">
          <span className="text-sm font-medium text-texto">Filtrar por data</span>
          <button onClick={onClose} className="w-6 h-6 flex items-center justify-center rounded-sm text-texto-3 hover:bg-superficie-2">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>
            </svg>
          </button>
        </div>

        {/* Navegação mês */}
        <div className="flex items-center justify-between mb-3">
          <span className="text-xs font-medium text-texto">{MESES[mes]} {ano}</span>
          <div className="flex gap-1">
            <button onClick={mesAnterior} className="w-6 h-6 flex items-center justify-center rounded border border-borda-forte text-texto-3 hover:bg-superficie-2">
              <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="15 18 9 12 15 6"/></svg>
            </button>
            <button onClick={proximoMes} className="w-6 h-6 flex items-center justify-center rounded border border-borda-forte text-texto-3 hover:bg-superficie-2">
              <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="9 18 15 12 9 6"/></svg>
            </button>
          </div>
        </div>

        {/* Grid calendário */}
        <div className="grid grid-cols-7 gap-0.5 mb-4">
          {DIAS_SEMANA.map((d, i) => (
            <div key={i} className="h-6 flex items-center justify-center text-[10px] font-medium text-texto-3">{d}</div>
          ))}
          {Array.from({ length: primeiroDia }).map((_, i) => <div key={`e${i}`} />)}
          {Array.from({ length: totalDias }).map((_, i) => {
            const d = i + 1
            const dataStr = formatarData(d)
            const futuro = isFuturo(d)
            const comSessao = temSessao(d)
            const sel = selecionado === dataStr
            const hoje_ = isHoje(d)

            return (
              <button
                key={d}
                onClick={() => handleDia(d)}
                disabled={futuro}
                className={`h-7 w-full flex items-center justify-center text-[11px] rounded relative transition-colors
                  ${futuro ? 'text-texto-4 cursor-default' : 'cursor-pointer'}
                  ${sel ? 'bg-accent-600 text-accent-fg' : ''}
                  ${!sel && comSessao ? 'font-medium text-texto hover:bg-superficie-2' : ''}
                  ${!sel && !comSessao && !futuro ? 'text-texto-3 hover:bg-superficie-2' : ''}
                  ${hoje_ && !sel ? 'ring-1 ring-accent-600 ring-inset' : ''}
                `}
              >
                {d}
                {comSessao && !sel && (
                  <span className="absolute bottom-0.5 left-1/2 -translate-x-1/2 w-1 h-1 rounded-full bg-accent-600" />
                )}
                {comSessao && sel && (
                  <span className="absolute bottom-0.5 left-1/2 -translate-x-1/2 w-1 h-1 rounded-full bg-white" />
                )}
              </button>
            )
          })}
        </div>

        {/* Botões */}
        <button
          onClick={handleApply}
          disabled={!selecionado || (selecionado !== hojeStr && !temSessao(diaNumSel))}
          className="w-full py-2 rounded-sm text-sm font-medium bg-accent-600 text-accent-fg hover:bg-accent-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors mb-2"
        >
          Ver informações
        </button>
        <button
          onClick={handleClear}
          className="w-full py-1.5 rounded-sm text-xs text-texto-2 border border-borda-forte hover:bg-superficie-2 transition-colors"
        >
          Hoje (ao vivo)
        </button>
      </div>
    </div>
  )
}
