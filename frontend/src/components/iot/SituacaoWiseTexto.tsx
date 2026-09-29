// Situação de um WISE, só em texto colorido (sem ícone): conectado em verde,
// desconectado em amarelo, em uso (em outra medição) em vermelho.
export type SituacaoWise = 'Conectado' | 'Desconectado' | 'EmUso'

const textos: Record<SituacaoWise, string> = {
  Conectado: 'conectado',
  Desconectado: 'desconectado',
  EmUso: 'em uso',
}

const cores: Record<SituacaoWise, string> = {
  Conectado: 'text-green-600 dark:text-green-400',
  Desconectado: 'text-amber-600 dark:text-amber-400',
  EmUso: 'text-red-600 dark:text-red-400',
}

interface Props {
  situacao: SituacaoWise
  className?: string
}

export default function SituacaoWiseTexto({ situacao, className = '' }: Props) {
  return <span className={`font-medium ${cores[situacao]} ${className}`}>{textos[situacao]}</span>
}
