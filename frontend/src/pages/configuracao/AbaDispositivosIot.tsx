// Aba "Dispositivos IoT" da tela de Configurações — só Administrador e Desenvolvedor.
// Cadastro dos WISE (a lista de aparelhos que podem ser usados numa medição Semi Auto) e
// diagnóstico da comunicação. O WISE não é de nenhuma máquina: é escolhido da lista ao
// iniciar a medição e fica livre ao finalizar.
// Adicionar: antes de gravar faz o ping no IP; se não responder, avisa e deixa adicionar
// mesmo assim (ex.: WISE que ainda vai ser instalado). Abaixo da lista, os WISE que estão
// publicando sem estar cadastrados. No topo, a situação do broker. Atualiza a cada 5 s.
import { useEffect, useState } from 'react'
import { useParametroUrl } from '../../hooks/useParametroUrl'
import { useAuth } from '../../contexts/AuthContext'
import { dispositivoIotService, type ResultadoPingDto, type StatusColetaIotDto, type WiseDto } from '../../services/dispositivoIotService'
import { mensagemErro } from '../../services/api'
import ValidarEntradasModal from '../../modals/ValidarEntradasModal'
import PingModal from '../../modals/PingModal'
import WiseModal from '../../modals/WiseModal'
import ConfirmModal from '../../components/ConfirmModal'
import SituacaoWiseTexto from '../../components/iot/SituacaoWiseTexto'
import { ipValido } from '../../components/iot/situacaoWise'
import { tempoDesde } from '../../utils/tempo'
import { btnPrimarySm, btnSecondarySm, btnIcon, btnIconDanger } from '../../styles/buttons'
import { inputBase } from '../../styles/inputs'
import { cardPadded } from '../../styles/cards'
import { table, tableHeadRow, tableHeadCell, tableBodyRow, tableBodyCell, tableBodyCellMuted, tableActionsCell } from '../../styles/tables'

const INTERVALO_MS = 5000

// Etapas do Adicionar: testando o ping, esperando decisão (não respondeu) ou gravando.
type EtapaAdicionar = 'parado' | 'testando' | 'semResposta' | 'salvando'

function resumoPing(p: ResultadoPingDto) {
  return `ping ${p.recebidos} de ${p.enviados}${p.tempoMedioMs !== null ? `, médio ${p.tempoMedioMs} ms` : ''}`
}

export default function AbaDispositivosIot() {
  const [wises, setWises] = useState<WiseDto[]>([])
  const [status, setStatus] = useState<StatusColetaIotDto | null>(null)
  const [carregado, setCarregado] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [agora, setAgora] = useState(new Date())
  const [recarregar, setRecarregar] = useState(0)

  // Adicionar
  const [novoIp, setNovoIp] = useState('')
  const [novoNome, setNovoNome] = useState('')
  const [etapa, setEtapa] = useState<EtapaAdicionar>('parado')
  const [pingNovo, setPingNovo] = useState<ResultadoPingDto | null>(null)
  // O que está sendo adicionado (do formulário ou do "Adicionar" de um WISE sem cadastro)
  const [emAdicao, setEmAdicao] = useState<{ ip: string; nome: string | null } | null>(null)
  const [erroAdicionar, setErroAdicionar] = useState<string | null>(null)
  const [adicionado, setAdicionado] = useState<string | null>(null)

  // Modais abertos na URL (?validar=, ?ping=, ?editar= com o IP) para o F5 reabri-los.
  // Situação do broker e contadores de mensagens: só para o Desenvolvedor.
  const { usuario } = useAuth()
  const desenvolvedor = usuario?.nivel === 'Desenvolvedor'

  const [validando, setValidando] = useParametroUrl('validar')
  const [pingando, setPingando] = useParametroUrl('ping')
  const [editarIp, setEditarIp] = useParametroUrl('editar')
  const [removendo, setRemovendo] = useState<WiseDto | null>(null)
  const [erroRemover, setErroRemover] = useState<string | null>(null)

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
  }, [recarregar])

  const cadastrados = wises.filter(w => w.cadastrado)
  const editando = editarIp ? cadastrados.find(w => w.enderecoIp === editarIp) ?? null : null
  const setEditando = (w: WiseDto | null) => setEditarIp(w?.enderecoIp ?? null)
  const semCadastro = wises.filter(w => !w.cadastrado)

  const ipNovoTexto = novoIp.trim()
  const ipNovoValido = ipValido(ipNovoTexto)
  const ipJaCadastrado = cadastrados.some(w => w.enderecoIp === ipNovoTexto)
  const ocupado = etapa === 'testando' || etapa === 'salvando'

  function editarIpNovo(ip: string) {
    setNovoIp(ip)
    setErroAdicionar(null)
    setAdicionado(null)
  }

  // Testa o ping e, se respondeu (ou se o usuário mandou adicionar mesmo assim), grava.
  async function adicionar(ip: string, nome: string | null, mesmoSemResposta: boolean) {
    setErroAdicionar(null)
    setAdicionado(null)
    setEmAdicao({ ip, nome })
    let ping = mesmoSemResposta ? pingNovo : null
    if (!mesmoSemResposta) {
      setEtapa('testando')
      setPingNovo(null)
      try {
        ping = await dispositivoIotService.ping(ip)
        setPingNovo(ping)
      } catch (e) {
        setErroAdicionar(mensagemErro(e, 'Não foi possível testar a conexão.'))
        setEtapa('parado')
        setEmAdicao(null)
        return
      }
      if (ping.recebidos === 0) {
        setEtapa('semResposta')
        return
      }
    }

    setEtapa('salvando')
    try {
      await dispositivoIotService.adicionar({ enderecoIp: ip, nome })
      setAdicionado(`WISE ${ip} adicionado${ping ? ` (${resumoPing(ping)})` : ''}.`)
      if (ip === novoIp.trim()) {
        setNovoIp('')
        setNovoNome('')
      }
      setPingNovo(null)
      setEmAdicao(null)
      setRecarregar(r => r + 1)
    } catch (e) {
      setErroAdicionar(mensagemErro(e, 'Não foi possível adicionar o WISE.'))
    } finally {
      setEtapa('parado')
    }
  }

  async function confirmarRemocao() {
    if (!removendo?.id) return
    try {
      await dispositivoIotService.remover(removendo.id)
      setRemovendo(null)
      setRecarregar(r => r + 1)
    } catch (e) {
      setRemovendo(null)
      setErroRemover(mensagemErro(e, 'Não foi possível remover o WISE.'))
    }
  }


  function acoesDiagnostico(w: WiseDto) {
    return (
      <>
        <button onClick={() => setValidando(w.enderecoIp)} className={btnSecondarySm}>Validar entradas</button>
        <button onClick={() => setPingando(w.enderecoIp)} className={btnSecondarySm}>Ping</button>
      </>
    )
  }

  return (
    <div className="flex flex-col gap-5">
      <div>
        <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">Dispositivos IoT (WISE)</p>
        <p className="text-[10px] text-zinc-400 mt-0.5">
          Os WISE cadastrados aqui podem ser escolhidos ao iniciar uma medição Semi Auto. O WISE fica associado à máquina só
          enquanto a medição dura e depois fica livre para outra.
        </p>
      </div>

      {erro && (
        <div className="bg-red-50 dark:bg-red-950 border border-red-200 dark:border-red-800 px-3 py-2 text-xs text-red-600 dark:text-red-400">
          {erro}
        </div>
      )}
      {erroRemover && (
        <div className="bg-red-50 dark:bg-red-950 border border-red-200 dark:border-red-800 px-3 py-2 text-xs text-red-600 dark:text-red-400 flex justify-between gap-3">
          <span>{erroRemover}</span>
          <button onClick={() => setErroRemover(null)} className="text-red-400 hover:text-red-600">fechar</button>
        </div>
      )}

      {/* Broker */}
      {status && (desenvolvedor
        ? <StatusBroker status={status} />
        : <AvisoComunicacao status={status} />)}

      {/* WISE cadastrados */}
      <div className={`${cardPadded} flex flex-col gap-3`}>
        <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">WISE cadastrados</p>

        {/* Adicionar: ping antes de gravar */}
        <div className="flex flex-col gap-2">
          <div className="flex flex-wrap items-center gap-2">
            <input
              value={novoIp}
              onChange={e => editarIpNovo(e.target.value)}
              placeholder="IP do WISE, ex.: 192.168.10.21"
              disabled={ocupado}
              className={`${inputBase} w-56`}
            />
            <input
              value={novoNome}
              onChange={e => setNovoNome(e.target.value)}
              placeholder="Nome (opcional), ex.: WISE 03"
              maxLength={60}
              disabled={ocupado}
              className={`${inputBase} w-56`}
            />
            <button
              onClick={() => adicionar(ipNovoTexto, novoNome.trim() || null, false)}
              disabled={!ipNovoValido || ipJaCadastrado || etapa !== 'parado'}
              className={btnPrimarySm}
            >
              {etapa === 'testando' ? 'Testando conexão...' : etapa === 'salvando' ? 'Adicionando...' : 'Adicionar'}
            </button>
          </div>

          {ipNovoTexto && !ipNovoValido && (
            <p className="text-[10px] text-red-600 dark:text-red-400">IP inválido. Use o formato 192.168.10.21.</p>
          )}
          {ipJaCadastrado && (
            <p className="text-[10px] text-red-600 dark:text-red-400">Este IP já está cadastrado.</p>
          )}
          {etapa === 'testando' && emAdicao && (
            <p className="text-[10px] text-zinc-400">Testando a conexão com {emAdicao.ip} (ping, 4 pacotes)...</p>
          )}
          {etapa === 'semResposta' && pingNovo && emAdicao && (
            <div className="bg-amber-50 dark:bg-amber-950 border border-amber-200 dark:border-amber-800 px-3 py-2 text-xs text-amber-700 dark:text-amber-400 flex flex-wrap items-center justify-between gap-3">
              <span>
                O WISE {emAdicao.ip} não respondeu ao ping ({pingNovo.recebidos} de {pingNovo.enviados}). Verifique a energia, o cabo e a rede,
                e se o IP está certo. Adicionar mesmo assim?
              </span>
              <span className="flex gap-2">
                <button onClick={() => { setEtapa('parado'); setEmAdicao(null) }} className={btnSecondarySm}>Cancelar</button>
                <button onClick={() => adicionar(emAdicao.ip, emAdicao.nome, true)} className={btnPrimarySm}>Adicionar mesmo assim</button>
              </span>
            </div>
          )}
          {erroAdicionar && <p className="text-[10px] text-red-600 dark:text-red-400">{erroAdicionar}</p>}
          {adicionado && <p className="text-[10px] text-green-600 dark:text-green-400">{adicionado}</p>}
        </div>

        {!carregado ? (
          <p className="text-xs text-zinc-400">Carregando...</p>
        ) : cadastrados.length === 0 ? (
          <p className="text-xs text-zinc-400">Nenhum WISE cadastrado.</p>
        ) : (
          <table className={table}>
            <thead>
              <tr className={tableHeadRow}>
                <th className={tableHeadCell}>Nome</th>
                <th className={tableHeadCell}>IP</th>
                <th className={tableHeadCell}>Identificação MQTT</th>
                <th className={tableHeadCell}>Conexão</th>
                <th className={tableHeadCell}>Última mensagem</th>
                <th className={tableHeadCell}>Medição</th>
                <th className="py-2" />
              </tr>
            </thead>
            <tbody>
              {cadastrados.map(w => (
                <tr key={w.enderecoIp} className={tableBodyRow}>
                  <td className={tableBodyCell}>{w.nome || <span className="text-zinc-400">—</span>}</td>
                  <td className={tableBodyCell}>{w.enderecoIp}</td>
                  <td className={tableBodyCellMuted}>{w.clientId || '—'}</td>
                  <td className="py-2.5 pr-4">
                    <SituacaoWiseTexto situacao={w.conectado ? 'Conectado' : 'Desconectado'} />
                  </td>
                  <td className={tableBodyCellMuted}>{tempoDesde(w.ultimaMensagemUtc, agora)}</td>
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
                    {acoesDiagnostico(w)}
                    <button onClick={() => setEditando(w)} title="Editar" className={btnIcon}>
                      <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
                    </button>
                    <button
                      onClick={() => setRemovendo(w)}
                      disabled={w.medicao !== null}
                      title={w.medicao ? 'Em medição: finalize antes de remover' : 'Remover'}
                      className={`${btnIconDanger} disabled:opacity-30 disabled:cursor-not-allowed`}
                    >
                      <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/></svg>
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Publicando sem cadastro */}
      {semCadastro.length > 0 && (
        <div className={cardPadded}>
          <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100 mb-1">Conectados sem cadastro</p>
          <p className="text-[10px] text-zinc-400 mb-3">
            Estes IPs estão se comunicando com o SmartLine, mas não estão na lista: não podem ser escolhidos numa medição até serem adicionados.
          </p>
          <table className={table}>
            <thead>
              <tr className={tableHeadRow}>
                <th className={tableHeadCell}>IP</th>
                <th className={tableHeadCell}>Identificação MQTT</th>
                <th className={tableHeadCell}>Conexão</th>
                <th className={tableHeadCell}>Última mensagem</th>
                <th className="py-2" />
              </tr>
            </thead>
            <tbody>
              {semCadastro.map(w => (
                <tr key={w.enderecoIp} className={tableBodyRow}>
                  <td className={tableBodyCell}>{w.enderecoIp}</td>
                  <td className={tableBodyCellMuted}>{w.clientId || '—'}</td>
                  <td className="py-2.5 pr-4">
                    <SituacaoWiseTexto situacao={w.conectado ? 'Conectado' : 'Desconectado'} />
                  </td>
                  <td className={tableBodyCellMuted}>{tempoDesde(w.ultimaMensagemUtc, agora)}</td>
                  <td className={tableActionsCell}>
                    {acoesDiagnostico(w)}
                    <button onClick={() => adicionar(w.enderecoIp, null, false)} disabled={etapa !== 'parado'} className={btnPrimarySm}>
                      Adicionar
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <ValidarEntradasModal
        open={validando !== null}
        subtitulo={validando ? `IP ${validando}` : undefined}
        carregar={() => dispositivoIotService.entradasDoIp(validando!)}
        onFechar={() => setValidando(null)}
      />

      <PingModal
        ip={pingando}
        onFechar={() => setPingando(null)}
      />

      {editando && (
        <WiseModal
          wise={editando}
          onFechar={() => setEditando(null)}
          onSalvo={() => { setEditando(null); setRecarregar(r => r + 1) }}
        />
      )}

      <ConfirmModal
        open={removendo !== null}
        titulo="Remover WISE"
        mensagem={removendo ? `Remover o WISE ${removendo.nome ? `"${removendo.nome}" ` : ''}(IP ${removendo.enderecoIp}) da lista? As medições já feitas com ele não mudam.` : ''}
        onConfirmar={confirmarRemocao}
        onCancelar={() => setRemovendo(null)}
      />
    </div>
  )
}

// Situação do broker MQTT embutido: se não estiver no ar, nenhum WISE consegue publicar.
// Para quem não é Desenvolvedor: nada quando está tudo certo; um aviso simples quando o
// sistema não consegue receber os WISE neste computador.
function AvisoComunicacao({ status }: { status: StatusColetaIotDto }) {
  if (status.brokerHabilitado && status.brokerEmExecucao) return null
  return (
    <div className="bg-red-50 dark:bg-red-950 border border-red-200 dark:border-red-800 px-3 py-2 text-xs text-red-600 dark:text-red-400">
      Este computador não está recebendo os WISE. Fale com o suporte.
    </div>
  )
}

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
