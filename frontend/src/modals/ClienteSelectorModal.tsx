// Seletor do topo: cliente (Administrador/Desenvolvedor) e linha de produção (todos). A linha
// escolhida vale para o Overview (mostra só ela) e para a Medição.
import { useState, useEffect, useRef } from 'react'
import type { Cliente, Linha } from '../types'

interface Props {
  open: boolean
  onClose: () => void
  onSelect: (cliente: Cliente) => void
  clientes: Cliente[]
  // false: usuário com cliente fixo — só escolhe a linha
  podeTrocarCliente: boolean
  linhas: Linha[]
  linhaId: string | null
  onSelectLinha: (linhaId: string) => void
  clienteAtualId: string | null
  anchorRef: React.RefObject<HTMLButtonElement | null>
}

export default function ClienteSelectorModal({ open, onClose, onSelect, clientes, podeTrocarCliente, linhas, linhaId, onSelectLinha, clienteAtualId, anchorRef }: Props) {
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

  // Trocar de cliente não fecha: a lista de linhas dele aparece embaixo para escolher
  function handleSelect(cliente: Cliente) {
    onSelect(cliente)
  }

  function handleSelectLinha(id: string) {
    onSelectLinha(id)
    onClose()
  }

  return (
    <div
      ref={popoverRef}
      style={{ position: 'fixed', top: pos.top, left: pos.left, zIndex: 50 }}
      className="w-96 bg-superficie border border-borda rounded-md shadow-lg"
    >
      <div
        style={{ position: 'absolute', top: -7, left: 14 }}
        className="w-3 h-3 bg-superficie border-l border-t border-borda rotate-45"
      />

      <div className="p-4">
        <div className="flex items-center justify-between mb-3">
          <span className="text-sm font-medium text-texto">{podeTrocarCliente ? 'Cliente e linha' : 'Linha de produção'}</span>
          <button onClick={onClose} className="w-6 h-6 flex items-center justify-center rounded text-texto-3 hover:bg-superficie-2">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>
            </svg>
          </button>
        </div>

        {podeTrocarCliente && (<>
        <div className="flex gap-2 mb-3">
          <select
            value={nome}
            onChange={e => { setNome(e.target.value); setEstado('') }}
            // min-w-0: um nome de cliente comprido não pode empurrar o Estado para fora do modal
            className="flex-1 min-w-0 h-8 px-2 rounded border border-borda-forte bg-campo text-xs text-texto focus:outline-none focus:ring-2 focus:ring-accent-500"
          >
            <option value="">Nome</option>
            {nomesDisponiveis.map(n => (
              <option key={n} value={n}>{n}</option>
            ))}
          </select>

          <select
            value={estado}
            onChange={e => { setEstado(e.target.value); setNome('') }}
            className="w-24 flex-shrink-0 h-8 px-2 rounded border border-borda-forte bg-campo text-xs text-texto focus:outline-none focus:ring-2 focus:ring-accent-500"
          >
            <option value="">Estado</option>
            {estadosDisponiveis.map(e => (
              <option key={e} value={e}>{e}</option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-0.5 max-h-48 overflow-y-auto rounded border border-borda-suave">
          {filtrados.length === 0 ? (
            <div className="py-4 text-center text-xs text-texto-3">
              Nenhum cliente encontrado
            </div>
          ) : (
            filtrados.map(c => (
              <button
                key={c.id}
                onClick={() => handleSelect(c)}
                className={`flex items-center justify-between px-3 py-2.5 text-left transition-colors first:rounded-t last:rounded-b ${
                  c.id === clienteAtualId ? 'bg-accent-50 dark:bg-accent-950' : 'hover:bg-superficie-2'
                }`}
              >
                <span className={`text-xs font-medium ${c.id === clienteAtualId ? 'text-accent-600 dark:text-accent-400' : 'text-texto'}`}>{c.nome}</span>
                {c.estado && (
                  <span className="text-[10px] text-texto-3 bg-superficie-3 px-1.5 py-0.5 rounded flex-shrink-0">
                    {c.estado}
                  </span>
                )}
              </button>
            ))
          )}
        </div>
        </>)}

        {/* Linhas do cliente atual */}
        <div className={podeTrocarCliente ? 'mt-3 pt-3 border-t border-borda' : ''}>
          {podeTrocarCliente && <p className="text-xs text-texto-2 mb-1.5">Linha de produção</p>}
          <div className="flex flex-col gap-0.5 max-h-40 overflow-y-auto rounded border border-borda-suave">
            {linhas.length === 0 ? (
              <div className="py-3 text-center text-xs text-texto-3">Nenhuma linha cadastrada</div>
            ) : (
              linhas.map(l => (
                <button
                  key={l.id}
                  onClick={() => handleSelectLinha(l.id)}
                  className={`flex items-center justify-between px-3 py-2 text-left transition-colors first:rounded-t last:rounded-b ${
                    l.id === linhaId ? 'bg-accent-50 dark:bg-accent-950 text-accent-600 dark:text-accent-400' : 'hover:bg-superficie-2 text-texto'
                  }`}
                >
                  <span className="text-xs font-medium">{l.nome}</span>
                  <span className="text-[10px] text-texto-3">{l.maquinas.length} máquina{l.maquinas.length === 1 ? '' : 's'}</span>
                </button>
              ))
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
