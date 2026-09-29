// Situação de uma máquina em coleta Semi Automática, em texto colorido (sem ícone).
import type { SituacaoColeta } from '../../services/coletaIotService'

const textos: Record<SituacaoColeta, string> = {
  Rodando: 'rodando',
  Parada: 'parada',
  SemComunicacao: 'sem comunicação',
  AguardandoPrimeiraAmostra: 'aguardando o WISE',
}

const cores: Record<SituacaoColeta, string> = {
  Rodando: 'text-green-600 dark:text-green-400',
  Parada: 'text-red-600 dark:text-red-400',
  SemComunicacao: 'text-zinc-500',
  AguardandoPrimeiraAmostra: 'text-amber-600 dark:text-amber-400',
}

interface Props {
  // Nulo: o motor ainda não pegou a coleta (acabou de ser iniciada)
  situacao: SituacaoColeta | null | undefined
  className?: string
}

export default function SituacaoColetaTexto({ situacao, className = '' }: Props) {
  if (!situacao) return <span className={`text-zinc-400 ${className}`}>iniciando</span>
  return <span className={`font-medium ${cores[situacao]} ${className}`}>{textos[situacao]}</span>
}
