// Aba "Dispositivos IoT" da tela de Configurações — só Administrador e Desenvolvedor.
// Diagnóstico da comunicação com os WISE: não há cadastro (o IP é informado ao iniciar a
// medição e o WISE fica livre ao finalizar). Lista todo WISE conectado, que publicou ou que
// está em medição, com a máquina que ele mede; para cada um (ou para um IP digitado) dá para
// validar as entradas ao vivo e fazer o teste de ping. No topo, a situação do broker MQTT
// embutido. Tudo se atualiza a cada 5 s.
import { useEffect, useState } from 'react'
import { dispositivoIotService, type StatusColetaIotDto, type WiseDto } from '../../services/dispositivoIotService'
import { mensagemErro } from '../../services/api'
import ValidarEntradasModal from '../../modals/ValidarEntradasModal'
import PingModal from '../../modals/PingModal'
import SituacaoWiseTexto from '../../components/iot/SituacaoWiseTexto'
import { ipValido } from '../../components/iot/situacaoWise'
import { tempoDesde } from '../../utils/tempo'
import { btnSecondarySm } from '../../styles/buttons'
import { inputBase } from '../../styles/inputs'
import { cardPadded } from '../../styles/cards'
import { table, tableHeadRow, tableHeadCell, tableBodyRow, tableBodyCell, tableBodyCellMuted, tableActionsCell } from '../../styles/tables'

const INTERVALO_MS = 5000

export default function AbaDispositivosIot() {
  const [wises, setWises] = useState<WiseDto[]>([])
  const [status, setStatus] = useState<StatusColetaIotDto | null>(null)
  const [carregado, setCarregado] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [agora, setAgora] = useState(new Date())

  const [ipTeste, setIpTeste] = useState('')
  const [validando, setValidando] = useState<string | null>(null)
  const [pingando, setPingando] = useState<string | null>(null)

  useEffect(() => {
    let ativo = true
    async function carregar() {
      try {
        const [w, s] = await Promise.all([dispositivoIotService.listar(), dispositivoIotService.status()])
        if (!ativo) return
        setWises(w)
        setStatus(s)
        setErro(null)
      } catch (e) {
        if (ativo) setErro(mensagemErro(e, 'Erro ao carregar os dispositivos.'))
      } finally {
        if (ativo) { setCarregado(true); setAgora(new Date()) }
      }
    }
    carregar()
    const id = setInterval(carregar, INTERVALO_MS)
    return () => { ativo = false; clearInterval(id) }
  }, [])

  const ipTesteValido = ipValido(ipTeste)
  // Conexão do IP em teste de ping, para o diagnóstico. A lista traz todo WISE conectado:
  // fora dela, não está conectado. Nula enquanto a lista não carregou.
  const pingadoConectado = pingando && carregado ? wises.some(w => w.enderecoIp === pingando && w.conectado) : null

  return (
    <div className="flex flex-col gap-5">
      <div>
        <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">Dispositivos IoT (WISE)</p>
        <p className="text-[10px] text-zinc-400 mt-0.5">
          Os WISE não são cadastrados: o IP é informado ao iniciar a medição Semi Auto e o WISE fica livre ao finalizar.
          Aqui dá para conferir a comunicação e as entradas de cada um.
        </p>
      </div>

      {erro && (
        <div className="bg-red-50 dark:bg-red-950 border border-red-200 dark:border-red-800 px-3 py-2 text-xs text-red-600 dark:text-red-400">
          {erro}
        </div>
      )}

      {/* Broker */}
      {status && <StatusBroker status={status} />}

      {/* Testar um IP qualquer (ex.: WISE que ainda não conectou) */}
      <div className={`${cardPadded} flex flex-col gap-2`}>
        <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">Testar um IP</p>
        <div className="flex flex-wrap items-center gap-2">
          <input
            value={ipTeste}
            onChange={e => setIpTeste(e.target.value)}
            placeholder="IP do WISE, ex.: 192.168.10.21"
            className={`${inputBase} w-64`}
          />
          <button onClick={() => setPingando(ipTeste.trim())} disabled={!ipTesteValido} className={btnSecondarySm}>Ping</button>
          <button onClick={() => setValidando(ipTeste.trim())} disabled={!ipTesteValido} className={btnSecondarySm}>Validar entradas</button>
        </div>
        {ipTeste.trim() && !ipTesteValido && (
          <p className="text-[10px] text-red-600 dark:text-red-400">IP inválido. Use o formato 192.168.10.21.</p>
        )}
        <p className="text-[10px] text-zinc-400">
          Para um WISE que não aparece na lista abaixo: o ping mostra se ele está na rede; se responde mas não conecta, confira a configuração MQTT dele.
        </p>
      </div>

      {/* WISE conhecidos */}
      {!carregado ? (
        <p className="text-xs text-zinc-400">Carregando...</p>
      ) : wises.length === 0 ? (
        <p className="text-xs text-zinc-400">
          Nenhum WISE conectado ou em medição. Um WISE aparece aqui assim que se conecta ao SmartLine.
        </p>
      ) : (
        <table className={table}>
          <thead>
            <tr className={tableHeadRow}>
              <th className={tableHeadCell}>IP</th>
              <th className={tableHeadCell}>Identificação MQTT</th>
              <th className={tableHeadCell}>Conexão</th>
              <th className={tableHeadCell}>Última mensagem</th>
              <th className={tableHeadCell}>Medição</th>
              <th className="py-2" />
            </tr>
          </thead>
          <tbody>
            {wises.map(w => (
              <tr key={w.enderecoIp} className={tableBodyRow}>
                <td className={tableBodyCell}>{w.enderecoIp}</td>
                <td className={tableBodyCellMuted}>{w.clientId || '—'}</td>
                <td className="py-2.5 pr-4">
                  <SituacaoWiseTexto situacao={w.conectado ? 'Conectado' : 'Desconectado'} />
                </td>
                <td className={tableBodyCellMuted}>
                  {tempoDesde(w.ultimaMensagemUtc, agora)}
                  {w.mensagens > 0 && <span className="block text-[10px] text-zinc-400">{w.mensagens.toLocaleString('pt-BR')} mensagens</span>}
                </td>
                <td className={tableBodyCellMuted}>
                  {w.medicao ? (
                    <>
                      <span className="text-blue-600 dark:text-blue-400">{w.medicao.maquina}</span>
                      <span className="block text-[10px] text-zinc-400">
                        {w.medicao.cliente} · {w.medicao.linha} · iniciada por {w.medicao.usuario} {tempoDesde(w.medicao.iniciadoEmUtc, agora)}
                      </span>
                    </>
                  ) : (
                    'livre'
                  )}
                </td>
                <td className={tableActionsCell}>
                  <button onClick={() => setValidando(w.enderecoIp)} className={btnSecondarySm}>Validar entradas</button>
                  <button onClick={() => setPingando(w.enderecoIp)} className={btnSecondarySm}>Ping</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <ValidarEntradasModal
        open={validando !== null}
        subtitulo={validando ? `IP ${validando}` : undefined}
        carregar={() => dispositivoIotService.entradasDoIp(validando!)}
        onFechar={() => setValidando(null)}
      />

      <PingModal
        ip={pingando}
        conectado={pingadoConectado}
        porta={status?.porta ?? null}
        onFechar={() => setPingando(null)}
      />
    </div>
  )
}

// Situação do broker MQTT embutido: se não estiver no ar, nenhum WISE consegue publicar.
function StatusBroker({ status }: { status: StatusColetaIotDto }) {
  const situacao = !status.brokerHabilitado
    ? { texto: 'desabilitado nesta instalação (MQTT_HABILITADO=false)', cor: 'text-red-600 dark:text-red-400' }
    : status.brokerEmExecucao
      ? { texto: `em funcionamento na porta ${status.porta}`, cor: 'text-green-600 dark:text-green-400' }
      : { texto: `não conseguiu abrir a porta ${status.porta} (outro programa usando ou firewall)`, cor: 'text-red-600 dark:text-red-400' }

  const numero = (n: number) => n.toLocaleString('pt-BR')

  return (
    <div className={`${cardPadded} flex flex-wrap items-center gap-x-6 gap-y-2 text-xs`}>
      <p className="text-zinc-500">Broker MQTT: <span className={`font-medium ${situacao.cor}`}>{situacao.texto}</span></p>
      <p className="text-zinc-500">WISE conectados: <span className="font-medium text-zinc-900 dark:text-zinc-100">{status.wiseConectados}</span></p>
      <p className="text-zinc-500">Mensagens processadas: <span className="text-zinc-900 dark:text-zinc-100">{numero(status.mensagensProcessadas)}</span></p>
      <p className="text-zinc-500" title="WISE livre (sem medição) ou mensagem fora do formato">
        Descartadas: <span className="text-zinc-900 dark:text-zinc-100">{numero(status.mensagensDescartadas)}</span>
      </p>
      {status.mensagensPerdidasFilaCheia > 0 && (
        <p className="text-red-600 dark:text-red-400">Perdidas por fila cheia: {numero(status.mensagensPerdidasFilaCheia)}</p>
      )}
    </div>
  )
}
