// Campo de texto com uma lista de valores já usados: ao focar, mostra as sugestões (filtradas
// pelo que foi digitado); cada sugestão tem um "x" para tirar da lista. O valor digitado é livre —
// a lista só poupa digitação.
import { useEffect, useRef, useState } from 'react'
import { inputMdFull } from '../styles/inputs'

interface Props {
  valor: string
  onChange: (valor: string) => void
  sugestoes: string[]
  onRemoverSugestao: (valor: string) => void
  placeholder?: string
  obrigatorio?: boolean
}

export default function CampoComLista({ valor, onChange, sugestoes, onRemoverSugestao, placeholder, obrigatorio }: Props) {
  const [aberto, setAberto] = useState(false)
  const caixaRef = useRef<HTMLDivElement>(null)

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

  const filtro = valor.trim().toLowerCase()
  const visiveis = sugestoes.filter(s => !filtro || s.toLowerCase().includes(filtro))
  const faltando = obrigatorio && !valor.trim()

  return (
    <div ref={caixaRef} className="relative">
      <input
        type="text"
        value={valor}
        onChange={e => { onChange(e.target.value); setAberto(true) }}
        onFocus={() => setAberto(true)}
        placeholder={placeholder}
        className={`${inputMdFull} ${faltando ? 'border-amber-400 dark:border-amber-600' : ''}`}
      />
      {aberto && visiveis.length > 0 && (
        <ul className="absolute z-20 left-0 right-0 mt-1 max-h-48 overflow-y-auto bg-superficie border border-borda-forte shadow-lg">
          {visiveis.map(s => (
            <li key={s} className="flex items-center group">
              <button
                type="button"
                onMouseDown={e => e.preventDefault()}
                onClick={() => { onChange(s); setAberto(false) }}
                className="flex-1 text-left px-3 py-1.5 text-sm text-texto hover:bg-superficie-2 truncate"
              >
                {s}
              </button>
              <button
                type="button"
                title="Remover da lista"
                onMouseDown={e => e.preventDefault()}
                onClick={() => onRemoverSugestao(s)}
                className="px-2 py-1.5 text-texto-3 hover:text-red-500 transition-colors"
              >
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>
                </svg>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
