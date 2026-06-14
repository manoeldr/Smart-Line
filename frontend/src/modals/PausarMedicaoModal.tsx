import { useState } from 'react'
import type { MotivoParadaDto } from '../services/maquinaService'

interface Props {
  open: boolean
  motivos: MotivoParadaDto[]
  loading: boolean
  onConfirmar: (motivoId: string) => void
  onCancelar: () => void
  onCadastrarNovo: (nome: string) => Promise<string>
}

export default function PausarMedicaoModal({ open, motivos, loading, onConfirmar, onCancelar, onCadastrarNovo }: Props) {
  const [selecionado, setSelecionado] = useState<string | null>(null)
  const [cadastrando, setCadastrando] = useState(false)
  const [novoNome, setNovoNome] = useState('')
  const [salvando, setSalvando] = useState(false)

  if (!open) return null

  const planejados = motivos.filter(m => m.tipo === 'Planejada')

  function handleConfirmar() {
    if (!selecionado) return
    setSelecionado(null)
    setCadastrando(false)
    setNovoNome('')
    onConfirmar(selecionado)
  }

  async function handleSalvarNovo() {
    if (!novoNome.trim()) return
    setSalvando(true)
    try {
      const novoId = await onCadastrarNovo(novoNome.trim())
      setSelecionado(novoId)
      setCadastrando(false)
      setNovoNome('')
    } finally {
      setSalvando(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 rounded-md shadow-lg w-80 p-5">

        {/* Header */}
        <div className="mb-4">
          <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">Pausar medição</p>
          <p className="text-xs text-zinc-400 mt-0.5">Selecione o motivo da pausa planejada</p>
        </div>

        {!cadastrando ? (
          <>
            {/* Lista de motivos planejados */}
            <div className="flex flex-col gap-1 max-h-52 overflow-y-auto mb-3">
              {loading ? (
                <p className="text-xs text-zinc-400 text-center py-4">Carregando...</p>
              ) : planejados.length === 0 ? (
                <p className="text-xs text-zinc-400 text-center py-4">Nenhum motivo planejado cadastrado</p>
              ) : (
                planejados.map(m => (
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
                    <span className="text-[10px] px-1.5 py-0.5 rounded flex-shrink-0 ml-2 bg-zinc-100 dark:bg-zinc-700 text-zinc-500">
                      planejada
                    </span>
                  </button>
                ))
              )}
            </div>

            {/* Cadastrar novo */}
            <button
              onClick={() => setCadastrando(true)}
              className="w-full flex items-center justify-center gap-1.5 py-1.5 mb-3 text-xs text-zinc-500 border border-dashed border-zinc-300 dark:border-zinc-700 rounded hover:bg-zinc-50 dark:hover:bg-zinc-800 transition-colors"
            >
              <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>
              </svg>
              Cadastrar novo motivo
            </button>

            {/* Botões */}
            <div className="grid grid-cols-2 gap-2">
              <button
                onClick={onCancelar}
                className="h-9 rounded border border-zinc-200 dark:border-zinc-700 text-xs text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800 transition-colors"
              >
                Cancelar
              </button>
              <button
                onClick={handleConfirmar}
                disabled={!selecionado}
                className="h-9 bg-blue-600 hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed text-white text-xs font-medium rounded transition-colors"
              >
                Pausar
              </button>
            </div>
          </>
        ) : (
          <>
            {/* Formulário novo motivo planejado */}
            <div className="flex flex-col gap-3 mb-4">
              <div className="flex flex-col gap-1.5">
                <label className="text-xs text-zinc-500">Descrição do motivo</label>
                <input
                  type="text"
                  value={novoNome}
                  onChange={e => setNovoNome(e.target.value)}
                  placeholder="Ex: Almoço, Setup, Reunião..."
                  autoFocus
                  className="h-9 px-3 rounded border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-sm text-zinc-900 dark:text-zinc-100 placeholder:text-zinc-400 focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
              </div>
              <div className="bg-zinc-50 dark:bg-zinc-800 rounded px-3 py-2 text-xs text-zinc-400">
                Motivos cadastrados aqui são do tipo <span className="font-medium text-zinc-600 dark:text-zinc-300">planejada</span> — não penalizam o OEE da máquina.
              </div>
            </div>

            <div className="grid grid-cols-2 gap-2">
              <button
                onClick={() => { setCadastrando(false); setNovoNome('') }}
                className="h-9 rounded border border-zinc-200 dark:border-zinc-700 text-xs text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800 transition-colors"
              >
                Cancelar
              </button>
              <button
                onClick={handleSalvarNovo}
                disabled={!novoNome.trim() || salvando}
                className="h-9 bg-blue-600 hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed text-white text-xs font-medium rounded transition-colors"
              >
                {salvando ? 'Salvando...' : 'Salvar'}
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  )
}
