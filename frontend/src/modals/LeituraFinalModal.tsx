import { useState } from 'react'
import type { CampoMaquinaDto } from '../services/configuracaoService'

interface Props {
  open: boolean
  camposExtras: CampoMaquinaDto[]
  onConfirmar: (producaoFinal: number, extras: { campoMaquinaId: string; valor: number }[]) => void
  onCancelar: () => void
  salvando?: boolean
}

export default function LeituraFinalModal({ open, camposExtras, onConfirmar, onCancelar, salvando }: Props) {
  const [producaoFinal, setProducaoFinal] = useState('')
  const [valoresExtras, setValoresExtras] = useState<Record<string, string>>({})

  if (!open) return null

  const inputCls = 'w-full h-9 px-3 border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-sm text-zinc-900 dark:text-zinc-100 focus:outline-none focus:ring-1 focus:ring-blue-500'

  function handleExtraChange(campoId: string, valor: string) {
    setValoresExtras(prev => ({ ...prev, [campoId]: valor }))
  }

  function handleConfirmar() {
    const producao = Number(producaoFinal)
    if (isNaN(producao) || producaoFinal === '') return

    const extras = camposExtras.map(c => ({
      campoMaquinaId: c.id,
      valor: Number(valoresExtras[c.id]) || 0,
    }))

    onConfirmar(producao, extras)
  }

  const podeConfirmar = producaoFinal !== '' && !isNaN(Number(producaoFinal))

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 w-96 max-h-[90vh] flex flex-col">

        <div className="px-5 py-4 border-b border-zinc-200 dark:border-zinc-800">
          <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">Leitura final</p>
          <p className="text-xs text-zinc-400 mt-0.5">Informe os valores finais antes de encerrar</p>
        </div>

        <div className="px-5 py-4 flex flex-col gap-4 overflow-y-auto">
          <div>
            <label className="text-xs text-zinc-500 mb-1 block">Produção final</label>
            <input
              type="number" min="0" autoFocus
              value={producaoFinal}
              onChange={e => setProducaoFinal(e.target.value)}
              placeholder="Leitura final do contador"
              className={inputCls}
            />
          </div>

          {camposExtras.map(c => (
            <div key={c.id}>
              <label className="text-xs text-zinc-500 mb-1 block">
                {c.nome} final {c.unidade && `(${c.unidade})`}
              </label>
              <input
                type="number"
                value={valoresExtras[c.id] ?? ''}
                onChange={e => handleExtraChange(c.id, e.target.value)}
                placeholder="Leitura final"
                className={inputCls}
              />
            </div>
          ))}
        </div>

        <div className="px-5 py-3 border-t border-zinc-200 dark:border-zinc-800 flex justify-end gap-2">
          <button
            onClick={onCancelar}
            disabled={salvando}
            className="h-8 px-4 border border-zinc-200 dark:border-zinc-700 text-xs text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800 disabled:opacity-50"
          >
            Cancelar
          </button>
          <button
            onClick={handleConfirmar}
            disabled={!podeConfirmar || salvando}
            className="h-8 px-4 bg-blue-600 hover:bg-blue-700 disabled:opacity-40 text-white text-xs font-medium"
          >
            {salvando ? 'Finalizando...' : 'Confirmar e finalizar'}
          </button>
        </div>
      </div>
    </div>
  )
}