// Situação do broker MQTT embutido e contadores de mensagens, para a Área do Desenvolvedor.
// Para os outros níveis, Dispositivos IoT mostra só um aviso quando o broker não está no ar.
import type { StatusColetaIotDto } from '../../services/dispositivoIotService'
import { cardPadded } from '../../styles/cards'

export default function StatusBroker({ status }: { status: StatusColetaIotDto }) {
  const situacao = !status.brokerHabilitado
    ? { texto: 'desabilitado nesta instalação (MQTT_HABILITADO=false)', cor: 'text-red-600 dark:text-red-400' }
    : status.brokerEmExecucao
      ? { texto: `em funcionamento na porta ${status.porta}`, cor: 'text-green-600 dark:text-green-400' }
      : { texto: `não conseguiu abrir a porta ${status.porta} (outro programa usando ou firewall)`, cor: 'text-red-600 dark:text-red-400' }

  const numero = (n: number) => n.toLocaleString('pt-BR')

  return (
    <div className={`${cardPadded} flex flex-wrap items-center gap-x-6 gap-y-2 text-xs`}>
      <p className="text-texto-2">Broker MQTT: <span className={`font-medium ${situacao.cor}`}>{situacao.texto}</span></p>
      <p className="text-texto-2">WISE conectados: <span className="font-medium text-texto">{status.wiseConectados}</span></p>
      <p className="text-texto-2">Mensagens processadas: <span className="text-texto">{numero(status.mensagensProcessadas)}</span></p>
      <p className="text-texto-2" title="WISE livre (sem medição) ou mensagem fora do formato">
        Descartadas: <span className="text-texto">{numero(status.mensagensDescartadas)}</span>
      </p>
      {status.mensagensPerdidasFilaCheia > 0 && (
        <p className="text-red-600 dark:text-red-400">Perdidas por fila cheia: {numero(status.mensagensPerdidasFilaCheia)}</p>
      )}
    </div>
  )
}
