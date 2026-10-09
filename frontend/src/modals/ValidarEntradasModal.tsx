// Validar entradas — mostra as 8 entradas de um WISE ao vivo, para conferir a fiação:
// o contador sobe quando a máquina produz, o sensor muda (0/1) quando é acionado.
// Quem abre passa a função que busca as entradas (por IP). Mostra se o WISE está conectado e
// se está em alguma medição ou livre. Atualiza a cada 2 s enquanto aberto.
import { useEffect, useEffectEvent, useState } from 'react'
import type { EntradaAoVivoDto, EntradasDoWiseDto } from '../services/dispositivoIotService'
import { mensagemErro } from '../services/api'
import SituacaoWiseTexto from '../components/iot/SituacaoWiseTexto'
import { tempoDesde } from '../utils/tempo'
import { btnSecondarySm } from '../styles/buttons'
import { modalOverlayNested, modalPanel, modalHeader, modalTitle, modalSubtitle, modalBody, modalFooter } from '../styles/modals'
import { table, tableHeadRow, tableHeadCell, tableBodyRow } from '../styles/tables'

interface Props {
  open: boolean
  titulo?: string
  subtitulo?: string
  carregar: () => Promise<EntradasDoWiseDto>
  onFechar: () => void
}

const INTERVALO_MS = 2000

function numero(n: number) {
  return n.toLocaleString('pt-BR')
}

export default function ValidarEntradasModal(props: Props) {
  // O conteúdo só existe aberto: cada abertura começa do zero (inclusive o "desde que abriu").
  return props.open ? <Conteudo {...props} /> : null
}

function Conteudo({ titulo = 'Validar entradas', subtitulo, carregar, onFechar }: Props) {
  const [situacao, setSituacao] = useState<EntradasDoWiseDto | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [agora, setAgora] = useState(new Date())

  // Quem abre recria a função a cada render; como evento, ela não reinicia o polling.
  const buscar = useEffectEvent(() => carregar())

  useEffect(() => {
    let ativo = true
    async function atualizar() {
      try {
        const s = await buscar()
        if (!ativo) return
        setSituacao(s)
        setErro(null)
      } catch (e) {
        if (ativo) setErro(mensagemErro(e, 'Não foi possível ler as entradas.'))
      } finally {
        if (ativo) setAgora(new Date())
      }
    }

    atualizar()
    const id = setInterval(atualizar, INTERVALO_MS)
    return () => { ativo = false; clearInterval(id) }
  }, [])

  // Valor atual da entrada: o número do contador, ou 0/1 no sensor (como chega do WISE).
  function status(e: EntradaAoVivoDto) {
    if (!e.recebida) return <span className="text-texto-3">sem leitura</span>
    const valor = e.tipo === 'Contador' ? numero(e.valor ?? 0) : e.valorBruto ? '1' : '0'
    return <span className="font-medium text-texto">{valor}</span>
  }

  return (
    <div className={modalOverlayNested}>
      <div className={`${modalPanel} w-[520px] max-h-[90vh]`}>
        <div className={modalHeader}>
          <p className={modalTitle}>{titulo}</p>
          {subtitulo && <p className={modalSubtitle}>{subtitulo}</p>}
        </div>

        <div className={modalBody}>
          {erro && (
            <div className="bg-red-50 dark:bg-red-950 border border-red-200 dark:border-red-800 px-3 py-2 text-xs text-red-600 dark:text-red-400">
              {erro}
            </div>
          )}

          {!situacao ? (
            !erro && <p className="text-xs text-texto-3">Carregando...</p>
          ) : (
            <>
              <div className="flex items-start justify-between gap-3 text-xs">
                <div>
                  <p className="text-texto-2">
                    WISE {situacao.enderecoIp} <SituacaoWiseTexto situacao={situacao.conectado ? 'Conectado' : 'Desconectado'} />
                  </p>
                  <p className="text-[10px] text-texto-3 mt-0.5">
                    {situacao.medicao
                      ? `Em medição na ${situacao.medicao.maquina} (${situacao.medicao.linha} · ${situacao.medicao.cliente}): nomes das entradas desta máquina.`
                      : 'Livre (sem medição): nomes padrão das entradas.'}
                  </p>
                </div>
                <p className="text-texto-3 text-right">
                  última mensagem: {situacao.ultimaMensagemUtc ? tempoDesde(situacao.ultimaMensagemUtc, agora) : 'nenhuma desde que o sistema iniciou'}
                </p>
              </div>

              <table className={table}>
                <thead>
                  <tr className={tableHeadRow}>
                    <th className={tableHeadCell}>Entrada</th>
                    <th className={tableHeadCell}>Nome</th>
                    <th className={tableHeadCell}>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {situacao.entradas.map(e => (
                    <tr key={e.canal} className={tableBodyRow}>
                      <td className="py-2 pr-4 text-texto-2 whitespace-nowrap">{e.canal} (DI{e.entrada})</td>
                      <td className="py-2 pr-4 text-texto">{e.nome}</td>
                      <td className="py-2 pr-4">{status(e)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>

              <p className="text-[10px] text-texto-3">
                "Sem leitura": o WISE ainda não enviou essa entrada. Confira a fiação e a configuração dela.
              </p>
            </>
          )}
        </div>

        <div className={modalFooter}>
          <button onClick={onFechar} className={btnSecondarySm}>Fechar</button>
        </div>
      </div>
    </div>
  )
}
