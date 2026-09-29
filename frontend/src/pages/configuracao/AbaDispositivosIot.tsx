// Aba "Dispositivos IoT" da tela de Configurações — só Administrador e Desenvolvedor.
// Instalação do Semi Automático: o WISE ligado na rede aparece em "Aguardando cadastro",
// o técnico confere as entradas (Validar entradas) e associa o IP à máquina.
// No topo, a situação do broker MQTT embutido. Tudo se atualiza a cada 5 s.
import { useEffect, useState } from 'react'
import {
  dispositivoIotService,
  type DispositivoIotDto,
  type StatusColetaIotDto,
  type WiseDesconhecidoDto,
} from '../../services/dispositivoIotService'
import { mensagemErro } from '../../services/api'
import DispositivoIotModal from '../../modals/DispositivoIotModal'
import ValidarEntradasModal from '../../modals/ValidarEntradasModal'
import ConfirmModal from '../../components/ConfirmModal'
import SituacaoWiseTexto from '../../components/iot/SituacaoWiseTexto'
import { tempoDesde } from '../../utils/tempo'
import { btnPrimarySm, btnPrimaryXs, btnSecondarySm, btnIcon, btnIconDanger } from '../../styles/buttons'
import { badgeStatus } from '../../styles/badges'
import { cardPadded } from '../../styles/cards'
import { table, tableHeadRow, tableHeadCell, tableBodyRow, tableBodyCell, tableBodyCellMuted, tableActionsCell } from '../../styles/tables'

const INTERVALO_MS = 5000

// Modal de cadastro: fechado, novo (com ou sem IP sugerido) ou editando.
type Edicao = { dispositivo: DispositivoIotDto | null; ipSugerido?: string } | null

export default function AbaDispositivosIot() {
  const [dispositivos, setDispositivos] = useState<DispositivoIotDto[]>([])
  const [desconhecidos, setDesconhecidos] = useState<WiseDesconhecidoDto[]>([])
  const [status, setStatus] = useState<StatusColetaIotDto | null>(null)
  const [carregado, setCarregado] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [agora, setAgora] = useState(new Date())
  const [recarregar, setRecarregar] = useState(0)

  const [edicao, setEdicao] = useState<Edicao>(null)
  const [validando, setValidando] = useState<{ ip: string; nome: string } | null>(null)
  const [excluindo, setExcluindo] = useState<DispositivoIotDto | null>(null)
  const [erroExclusao, setErroExclusao] = useState<string | null>(null)

  useEffect(() => {
    let ativo = true
    async function carregar() {
      try {
        const [d, x, s] = await Promise.all([
          dispositivoIotService.listar(),
          dispositivoIotService.desconhecidos(),
          dispositivoIotService.status(),
        ])
        if (!ativo) return
        setDispositivos(d)
        setDesconhecidos(x)
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
  }, [recarregar])

  async function confirmarExclusao() {
    if (!excluindo) return
    try {
      await dispositivoIotService.excluir(excluindo.id)
      setExcluindo(null)
      setRecarregar(r => r + 1)
    } catch (e) {
      setExcluindo(null)
      setErroExclusao(mensagemErro(e, 'Não foi possível excluir o WISE.'))
    }
  }

  function aoSalvar() {
    setEdicao(null)
    setRecarregar(r => r + 1)
  }

  return (
    <div className="flex flex-col gap-5">
      {/* Cabeçalho */}
      <div className="flex items-center justify-between">
        <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">Dispositivos IoT (WISE)</p>
        <button onClick={() => setEdicao({ dispositivo: null })} className={`${btnPrimarySm} flex items-center gap-1.5`}>
          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
          Novo WISE
        </button>
      </div>

      {erro && (
        <div className="bg-red-50 dark:bg-red-950 border border-red-200 dark:border-red-800 px-3 py-2 text-xs text-red-600 dark:text-red-400">
          {erro}
        </div>
      )}
      {erroExclusao && (
        <div className="bg-red-50 dark:bg-red-950 border border-red-200 dark:border-red-800 px-3 py-2 text-xs text-red-600 dark:text-red-400 flex justify-between gap-3">
          <span>{erroExclusao}</span>
          <button onClick={() => setErroExclusao(null)} className="text-red-400 hover:text-red-600">fechar</button>
        </div>
      )}

      {/* Broker */}
      {status && <StatusBroker status={status} />}

      {/* WISE aguardando cadastro */}
      {desconhecidos.length > 0 && (
        <div className={cardPadded}>
          <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100 mb-1">WISE aguardando cadastro</p>
          <p className="text-[10px] text-zinc-400 mb-3">
            Estes IPs estão publicando no SmartLine, mas não estão associados a nenhuma máquina. As mensagens deles são ignoradas até o cadastro.
          </p>
          <table className={table}>
            <thead>
              <tr className={tableHeadRow}>
                <th className={tableHeadCell}>IP</th>
                <th className={tableHeadCell}>Identificação MQTT</th>
                <th className={tableHeadCell}>Última mensagem</th>
                <th className="py-2" />
              </tr>
            </thead>
            <tbody>
              {desconhecidos.map(w => (
                <tr key={w.enderecoIp} className={tableBodyRow}>
                  <td className={tableBodyCell}>{w.enderecoIp}</td>
                  <td className={tableBodyCellMuted}>{w.clientId || '—'}</td>
                  <td className={tableBodyCellMuted}>{tempoDesde(w.ultimaMensagemUtc, agora)}</td>
                  <td className={tableActionsCell}>
                    <button onClick={() => setValidando({ ip: w.enderecoIp, nome: 'WISE sem cadastro' })} className={btnSecondarySm}>
                      Validar entradas
                    </button>
                    <button onClick={() => setEdicao({ dispositivo: null, ipSugerido: w.enderecoIp })} className={btnPrimaryXs}>
                      Cadastrar
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* WISE cadastrados */}
      {!carregado ? (
        <p className="text-xs text-zinc-400">Carregando...</p>
      ) : dispositivos.length === 0 ? (
        <p className="text-xs text-zinc-400">Nenhum WISE cadastrado.</p>
      ) : (
        <table className={table}>
          <thead>
            <tr className={tableHeadRow}>
              <th className={tableHeadCell}>Nome</th>
              <th className={tableHeadCell}>IP</th>
              <th className={tableHeadCell}>Máquina</th>
              <th className={tableHeadCell}>Conexão</th>
              <th className={tableHeadCell}>Última mensagem</th>
              <th className={tableHeadCell}>Coleta</th>
              <th className={tableHeadCell}>Status</th>
              <th className="py-2" />
            </tr>
          </thead>
          <tbody>
            {dispositivos.map(d => (
              <tr key={d.id} className={tableBodyRow}>
                <td className={tableBodyCell}>{d.nome}</td>
                <td className={tableBodyCellMuted}>{d.enderecoIp}</td>
                <td className={tableBodyCellMuted}>
                  {d.maquina}
                  <span className="block text-[10px] text-zinc-400">{d.cliente} · {d.linha}</span>
                </td>
                <td className="py-2.5 pr-4">
                  {d.ativo
                    ? <SituacaoWiseTexto situacao={d.conectado ? 'Conectado' : 'Desconectado'} />
                    : <span className="text-zinc-400">—</span>}
                </td>
                <td className={tableBodyCellMuted}>{tempoDesde(d.ultimaMensagemEm, agora)}</td>
                <td className={tableBodyCellMuted}>
                  {d.coletaEmAndamento ? <span className="text-blue-600 dark:text-blue-400">em andamento</span> : '—'}
                </td>
                <td className="py-2.5 pr-4">
                  <span className={badgeStatus(d.ativo)}>{d.ativo ? 'ativo' : 'inativo'}</span>
                </td>
                <td className={tableActionsCell}>
                  <button onClick={() => setValidando({ ip: d.enderecoIp, nome: d.nome })} className={btnSecondarySm}>
                    Validar entradas
                  </button>
                  <button onClick={() => setEdicao({ dispositivo: d })} title="Editar" className={btnIcon}>
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
                  </button>
                  <button onClick={() => setExcluindo(d)} title="Excluir" className={btnIconDanger}>
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/></svg>
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {edicao && (
        <DispositivoIotModal
          dispositivo={edicao.dispositivo}
          ipSugerido={edicao.ipSugerido}
          dispositivos={dispositivos}
          onFechar={() => setEdicao(null)}
          onSalvo={aoSalvar}
        />
      )}

      <ValidarEntradasModal
        open={validando !== null}
        subtitulo={validando ? `${validando.nome} · IP ${validando.ip}` : undefined}
        carregar={() => dispositivoIotService.entradasDoIp(validando!.ip)}
        onFechar={() => setValidando(null)}
      />

      <ConfirmModal
        open={excluindo !== null}
        titulo="Excluir WISE"
        mensagem={excluindo ? `Excluir o WISE "${excluindo.nome}" (IP ${excluindo.enderecoIp})? As mensagens desse IP passam a ser ignoradas.` : ''}
        onConfirmar={confirmarExclusao}
        onCancelar={() => setExcluindo(null)}
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
      <p className="text-zinc-500" title="IP sem cadastro, máquina sem coleta ligada ou mensagem fora do formato">
        Descartadas: <span className="text-zinc-900 dark:text-zinc-100">{numero(status.mensagensDescartadas)}</span>
      </p>
      {status.mensagensPerdidasFilaCheia > 0 && (
        <p className="text-red-600 dark:text-red-400">Perdidas por fila cheia: {numero(status.mensagensPerdidasFilaCheia)}</p>
      )}
    </div>
  )
}
