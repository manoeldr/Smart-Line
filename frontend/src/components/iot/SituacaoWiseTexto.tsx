// Situação da conexão de um WISE, só em texto colorido (sem ícone):
// conectado em verde, desconectado em amarelo, não cadastrado em vermelho.
import type { SituacaoConexaoWise } from '../../services/dispositivoIotService'

const textos: Record<SituacaoConexaoWise, string> = {
  Conectado: 'conectado',
  Desconectado: 'desconectado',
  NaoCadastrado: 'não cadastrado',
}

const cores: Record<SituacaoConexaoWise, string> = {
  Conectado: 'text-green-600 dark:text-green-400',
  Desconectado: 'text-amber-600 dark:text-amber-400',
  NaoCadastrado: 'text-red-600 dark:text-red-400',
}

interface Props {
  situacao: SituacaoConexaoWise
  className?: string
}

export default function SituacaoWiseTexto({ situacao, className = '' }: Props) {
  return <span className={`font-medium ${cores[situacao]} ${className}`}>{textos[situacao]}</span>
}
