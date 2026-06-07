import { useState } from 'react'
import type { MotivoParadaDto } from '../services/maquinaService'

interface Props {
  open: boolean
  motivos: MotivoParadaDto[]
  loading: boolean
  onConfirmar: (motivoId: string) => void
  onCadastrarNovo: () => void
}

export default function MotivoParadaModal({ open, motivos, loading, onConfirmar, onCadastrarNovo }: Props) {
  const [selecionado, setSelecionado] = useState<string | null>(null)
  const [filtro, setFiltro] = useState<'todos' | 'Interna' | 'Externa'>('todos')

  if (!open) return null

  const filtrados = motivos.filter(m =>
    m.tipo !== 'Planejada' && (filtro === 'todos' || m.tipo === filtro)
  )

  const tipoLabel: Record<string, string> = {
    Interna: 'interna',
    Externa: 'externa',
  }

  const tipoCor: Record<string, string> = {
    Interna: 'bg-blue-50 dark:bg-blue-950 text-blue-600 dark:text-blue-400',
    Externa: 'bg-amber-50 dark:bg-amber-950 text-amber-700 dark:text-amber-400',
  }

  function handleConfirmar() {
    if (!selecionado) return
    setSelecionado(null)
    onConfirmar(selecionado)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 rounded-md shadow-lg w-80 p-5">

        {/* Header */}
        <div className="mb-4">
          <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">Motivo da parada</p>
          <p className="text-xs text-zinc-400 mt-0.5">Selecione o motivo que causou a parada</p>
        </div>

        {/* Filtro por tipo */}
        <div className="flex gap-1.5 mb-3">
          {(['todos', 'Interna', 'Externa'] as const).map(t => (
            <button
              key={t}
              onClick={() => setFiltro(t)}
              className={`text-[10px] px-2.5 py-1 rounded border transition-colors ${
                filtro === t
                  ? 'bg-blue-600 text-white border-blue-600'
                  : 'border-zinc-200 dark:border-zinc-700 text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800'
              }`}
            >
              {t === 'todos' ? 'Todos' : t === 'Interna' ? 'Internas' : 'Externas'}
            </button>
          ))}
        </div>

        {/* Lista de motivos */}
        <div className="flex flex-col gap-1 max-h-52 overflow-y-auto mb-3">
          {loading ? (
            <p className="text-xs text-zinc-400 text-center py-4">Carregando...</p>
          ) : filtrados.length === 0 ? (
            <p className="text-xs text-zinc-400 text-center py-4">Nenhum motivo cadastrado</p>
          ) : (
            filtrados.map(m => (
              <button
                key={m.id}
                onClick={() => setSelecionado(m.id)}
                className={`flex items-center justify-between px-3 py-2 rounded border text-left transition-colors ${
                  selecionado === m.id
                    ? 'border-blue-500 bg-blue-50 dark:bg-blue-950'
                    : 'border-zinc-100 dark:border-zinc-800 hover:bg-zinc-50 dark:hover:bg-zinc-800'
                }`}
              >
                <span className="text-xs text-zinc-900 dark:text-zinc-100">{m.nome}</span>
                <span className={`text-[10px] px-1.5 py-0.5 rounded flex-shrink-0 ml-2 ${tipoCor[m.tipo] ?? ''}`}>
                  {tipoLabel[m.tipo] ?? m.tipo}
                </span>
              </button>
            ))
          )}
        </div>

        {/* Cadastrar novo */}
        <button
          onClick={onCadastrarNovo}
          className="w-full flex items-center justify-center gap-1.5 py-1.5 mb-3 text-xs text-zinc-500 border border-dashed border-zinc-300 dark:border-zinc-700 rounded hover:bg-zinc-50 dark:hover:bg-zinc-800 transition-colors"
        >
          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>
          </svg>
          Cadastrar novo motivo
        </button>

        {/* Confirmar */}
        <button
          onClick={handleConfirmar}
          disabled={!selecionado}
          className="w-full h-9 bg-blue-600 hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed text-white text-sm font-medium rounded transition-colors"
        >
          Confirmar
        </button>
      </div>
    </div>
  )
}