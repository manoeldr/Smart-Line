import { useState, useEffect, useRef } from 'react'
import type { Cliente } from '../types'

interface Props {
  open: boolean
  onClose: () => void
  onSelect: (cliente: Cliente) => void
  clientes: Cliente[]
  anchorRef: React.RefObject<HTMLButtonElement | null>
}

export default function ClienteSelectorModal({ open, onClose, onSelect, clientes, anchorRef }: Props) {
  const [nome, setNome] = useState('')
  const [estado, setEstado] = useState('')
  const [pos, setPos] = useState({ top: 0, left: 0 })
  const popoverRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (open && anchorRef.current) {
      const rect = anchorRef.current.getBoundingClientRect()
      setPos({ top: rect.bottom + 10, left: rect.left })
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

  const clientesFiltradosPorNome = nome === ''
    ? clientes
    : clientes.filter(c => c.nome === nome)

  const estadosDisponiveis = [...new Set(
    clientesFiltradosPorNome.map(c => c.estado).filter(Boolean) as string[]
  )].sort()

  const clientesFiltradosPorEstado = estado === ''
    ? clientes
    : clientes.filter(c => c.estado === estado)

  const nomesDisponiveis = [...new Set(
    clientesFiltradosPorEstado.map(c => c.nome)
  )].sort()

  const filtrados = clientes.filter(c =>
    (nome === '' || c.nome === nome) &&
    (estado === '' || c.estado === estado)
  )

  function handleSelect(cliente: Cliente) {
    onSelect(cliente)
    onClose()
  }

  return (
    <div
      ref={popoverRef}
      style={{ position: 'fixed', top: pos.top, left: pos.left, zIndex: 50 }}
      className="w-72 bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 rounded-md shadow-lg"
    >
      <div
        style={{ position: 'absolute', top: -7, left: 14 }}
        className="w-3 h-3 bg-white dark:bg-zinc-900 border-l border-t border-zinc-200 dark:border-zinc-800 rotate-45"
      />

      <div className="p-4">
        <div className="flex items-center justify-between mb-3">
          <span className="text-sm font-medium text-zinc-900 dark:text-zinc-100">Selecionar cliente</span>
          <button onClick={onClose} className="w-6 h-6 flex items-center justify-center rounded text-zinc-400 hover:bg-zinc-100 dark:hover:bg-zinc-800">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>
            </svg>
          </button>
        </div>

        <div className="flex gap-2 mb-3">
          <select
            value={nome}
            onChange={e => { setNome(e.target.value); setEstado('') }}
            className="flex-1 h-8 px-2 rounded border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-xs text-zinc-900 dark:text-zinc-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="">Nome</option>
            {nomesDisponiveis.map(n => (
              <option key={n} value={n}>{n}</option>
            ))}
          </select>

          <select
            value={estado}
            onChange={e => { setEstado(e.target.value); setNome('') }}
            className="flex-1 h-8 px-2 rounded border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-xs text-zinc-900 dark:text-zinc-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="">Estado</option>
            {estadosDisponiveis.map(e => (
              <option key={e} value={e}>{e}</option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-0.5 max-h-48 overflow-y-auto rounded border border-zinc-100 dark:border-zinc-800">
          {filtrados.length === 0 ? (
            <div className="py-4 text-center text-xs text-zinc-400">
              Nenhum cliente encontrado
            </div>
          ) : (
            filtrados.map(c => (
              <button
                key={c.id}
                onClick={() => handleSelect(c)}
                className="flex items-center justify-between px-3 py-2.5 text-left hover:bg-zinc-50 dark:hover:bg-zinc-800 transition-colors first:rounded-t last:rounded-b"
              >
                <span className="text-xs font-medium text-zinc-900 dark:text-zinc-100">{c.nome}</span>
                {c.estado && (
                  <span className="text-[10px] text-zinc-400 bg-zinc-100 dark:bg-zinc-700 px-1.5 py-0.5 rounded flex-shrink-0">
                    {c.estado}
                  </span>
                )}
              </button>
            ))
          )}
        </div>
      </div>
    </div>
  )
}
