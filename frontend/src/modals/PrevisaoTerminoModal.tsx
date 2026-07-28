// Modal global de aviso quando a previsão de término da medição é atingida.
// Renderizado pelo Watcher (components/SessaoGlobal), fica visível em qualquer tela do sistema.
// Contador regressivo de 5 minutos: se ninguém responder, finaliza a sessão automaticamente.
import { useState, useEffect } from 'react'
import { btnPrimary, btnSecondary } from '../styles/buttons'
import { inputMdFull, label } from '../styles/inputs'
import { modalOverlayDark } from '../styles/modals'

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

  return (
    <div className={modalOverlayDark}>
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
          <label className={label}>Novo horário de término (para estender)</label>
          <input
            type="time"
            value={novaHora}
            onChange={e => setNovaHora(e.target.value)}
            className={inputMdFull}
          />
        </div>

        <div className="grid grid-cols-2 gap-2">
          <button onClick={onFinalizar} className={btnSecondary}>
            Finalizar agora
          </button>
          <button onClick={() => novaHora && onEstender(novaHora)} disabled={!novaHora} className={btnPrimary}>
            Estender
          </button>
        </div>
      </div>
    </div>
  )
}