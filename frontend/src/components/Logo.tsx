// Marca do Smart Line Scan: engrenagem branca num quadrado preto (no escuro, com um anel
// cinza para o quadrado não sumir no fundo). Usada no menu lateral, no login e na ativação.
const ENGRENAGEM =
  'M10.12 3.61 L10.45 1.11 L13.55 1.11 L13.88 3.61 L16.61 4.74 L18.60 3.20 L20.80 5.40 L19.26 7.39 ' +
  'L20.39 10.12 L22.89 10.45 L22.89 13.55 L20.39 13.88 L19.26 16.61 L20.80 18.60 L18.60 20.80 L16.61 19.26 ' +
  'L13.88 20.39 L13.55 22.89 L10.45 22.89 L10.12 20.39 L7.39 19.26 L5.40 20.80 L3.20 18.60 L4.74 16.61 ' +
  'L3.61 13.88 L1.11 13.55 L1.11 10.45 L3.61 10.12 L4.74 7.39 L3.20 5.40 L5.40 3.20 L7.39 4.74 Z ' +
  'M16.6 12 a4.6 4.6 0 1 0 -9.2 0 a4.6 4.6 0 1 0 9.2 0 Z'

interface Props {
  /** Lado do quadrado, em px (o desenho ocupa metade). */
  tamanho?: number
  className?: string
}

export default function Logo({ tamanho = 32, className = '' }: Props) {
  const desenho = Math.round(tamanho / 2)
  return (
    <div
      className={`bg-black dark:ring-1 dark:ring-zinc-700 flex items-center justify-center flex-shrink-0 ${tamanho >= 40 ? 'rounded-xl' : 'rounded-lg'} ${className}`}
      style={{ width: tamanho, height: tamanho }}
      aria-label="Smart Line Scan"
    >
      <svg width={desenho} height={desenho} viewBox="0 0 24 24" aria-hidden="true">
        <path d={ENGRENAGEM} fill="white" fillRule="evenodd" stroke="white" strokeWidth="0.6" strokeLinejoin="round" />
      </svg>
    </div>
  )
}
