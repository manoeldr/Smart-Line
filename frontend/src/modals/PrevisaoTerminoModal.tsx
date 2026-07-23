import { useState, useEffect } from 'react'

interface Props {
  open: boolean
  onEstender: (novaHora: string) => void
  onFinalizar: () => void
}

export default function PrevisaoTerminoModal({ open, onEstender, onFinalizar }: Props) {
  const [segundosRestantes, setSegundosRestantes] = useState(5 * 60)
  const [novaHora, setNovaHora] = useState('')

  useEffect(() => {
    if (!open) {
      setSegundosRestantes(5 * 60)
      setNovaHora('')
      return
    }

    const id = setInterval(() => {
      setSegundosRestantes(s => {
        if (s <= 1) {
          clearInterval(id)
          onFinalizar()
          return 0
        }
        return s - 1
      })
    }, 1000)

    return () => clearInterval(id)
  }, [open, onFinalizar])

  if (!open) return null

  const minutos = String(Math.floor(segundosRestantes / 60)).padStart(2, '0')
  const segundos = String(segundosRestantes % 60).padStart(2, '0')

  const inputCls = 'w-full h-9 px-3 border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-sm text-zinc-900 dark:text-zinc-100 focus:outline-none focus:ring-1 focus:ring-blue-500'

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="bg-white dark:bg-zinc-900 border border-amber-300 dark:border-amber-700 w-96 p-6">

        <div className="text-center mb-4">
          <div className="inline-flex items-center justify-center w-12 h-12 bg-amber-50 dark:bg-amber-950 rounded-full mb-3">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#d97706" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/>
            </svg>
          </div>
          <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">
            Previsão de término atingida
          </p>
          <p className="text-xs text-zinc-500 mt-1">
            A medição será finalizada automaticamente em
          </p>
          <p className="text-3xl font-medium text-amber-600 dark:text-amber-400 mt-2 tabular-nums">
            {minutos}:{segundos}
          </p>
        </div>

        <div className="mb-4">
          <label className="text-xs text-zinc-500 mb-1 block">Novo horário de término (para estender)</label>
          <input
            type="time"
            value={novaHora}
            onChange={e => setNovaHora(e.target.value)}
            className={inputCls}
          />
        </div>

        <div className="grid grid-cols-2 gap-2">
          <button
            onClick={onFinalizar}
            className="h-9 border border-zinc-200 dark:border-zinc-700 text-sm text-zinc-700 dark:text-zinc-300 hover:bg-zinc-50 dark:hover:bg-zinc-800"
          >
            Finalizar agora
          </button>
          <button
            onClick={() => novaHora && onEstender(novaHora)}
            disabled={!novaHora}
            className="h-9 bg-blue-600 hover:bg-blue-700 disabled:opacity-40 text-white text-sm font-medium"
          >
            Estender
          </button>
        </div>
      </div>
    </div>
  )
}