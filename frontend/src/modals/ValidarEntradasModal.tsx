// Validar entradas — mostra as 8 entradas de um WISE ao vivo, em texto, para conferir a
// fiação: o contador sobe quando a máquina produz, o sensor muda quando é acionado.
// Reutilizável: quem abre passa a função que busca a situação (por IP, na aba Dispositivos
// IoT, ou pela máquina, no Configurar medição). Atualiza a cada 2 s enquanto aberto.
import { useEffect, useEffectEvent, useState } from 'react'
import type { EntradaAoVivoDto, SituacaoWiseDto } from '../services/dispositivoIotService'
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
  carregar: () => Promise<SituacaoWiseDto>
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
  const [situacao, setSituacao] = useState<SituacaoWiseDto | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [agora, setAgora] = useState(new Date())

  // Primeiro valor visto de cada contador desde que o modal abriu: "subiu N desde que abriu".
  const [inicial, setInicial] = useState<Record<string, number>>({})

  // Quem abre recria a função a cada render; como evento, ela não reinicia o polling.
  const buscar = useEffectEvent(() => carregar())

  useEffect(() => {
    let ativo = true
    async function atualizar() {
      try {
        const s = await buscar()
        if (!ativo) return
        setInicial(anterior => {
          const novo = { ...anterior }
          for (const e of s.entradas) {
            if (e.tipo === 'Contador' && e.valor !== null && novo[e.canal] === undefined) novo[e.canal] = e.valor
          }
          return novo
        })
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

  function leitura(e: EntradaAoVivoDto) {
    if (!e.recebida) return <span className="text-zinc-400">sem leitura</span>
    if (e.tipo === 'Contador') {
      return <span className="font-medium text-zinc-900 dark:text-zinc-100">{numero(e.valor ?? 0)}</span>
    }
    return (
      <span className={e.emAlarme ? 'text-amber-600 dark:text-amber-400 font-medium' : 'text-green-600 dark:text-green-400'}>
        {e.texto}
      </span>
    )
  }

  function variacao(e: EntradaAoVivoDto) {
    if (!e.recebida) return null
    if (e.tipo === 'Estado') {
      return <span className="text-zinc-400">{e.emAlarme ? 'ativo' : 'normal'} (entrada = {e.valorBruto ? 1 : 0})</span>
    }
    const partes: string[] = []
    if (e.incremento !== null && e.intervaloSegundos !== null) {
      partes.push(`+${numero(e.incremento)} em ${Math.round(e.intervaloSegundos)} s`)
    }
    const base = inicial[e.canal]
    if (base !== undefined && e.valor !== null && e.valor >= base) {
      partes.push(`+${numero(e.valor - base)} desde que abriu`)
    }
    return <span className="text-zinc-500">{partes.join(' · ') || '—'}</span>
  }

  return (
    <div className={modalOverlayNested}>
      <div className={`${modalPanel} w-[620px] max-h-[90vh]`}>
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
            !erro && <p className="text-xs text-zinc-400">Carregando...</p>
          ) : (
            <>
              <div className="flex items-center justify-between text-xs">
                <p className="text-zinc-500">
                  WISE {situacao.enderecoIp ? `${situacao.enderecoIp} ` : ''}
                  <SituacaoWiseTexto situacao={situacao.situacao} />
                </p>
                <p className="text-zinc-400">
                  última mensagem: {situacao.ultimaMensagemUtc ? tempoDesde(situacao.ultimaMensagemUtc, agora) : 'nenhuma desde que o sistema iniciou'}
                </p>
              </div>

              <table className={table}>
                <thead>
                  <tr className={tableHeadRow}>
                    <th className={tableHeadCell}>Entrada</th>
                    <th className={tableHeadCell}>Nome</th>
                    <th className={tableHeadCell}>Leitura</th>
                    <th className={tableHeadCell}>Variação</th>
                  </tr>
                </thead>
                <tbody>
                  {situacao.entradas.map(e => (
                    <tr key={e.canal} className={tableBodyRow}>
                      <td className="py-2 pr-4 text-zinc-500 whitespace-nowrap">{e.canal} (DI{e.entrada})</td>
                      <td className="py-2 pr-4 text-zinc-900 dark:text-zinc-100">{e.nome}</td>
                      <td className="py-2 pr-4">{leitura(e)}</td>
                      <td className="py-2 pr-4">{variacao(e)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>

              <p className="text-[10px] text-zinc-400">
                Os contadores chegam no intervalo de publicação configurado no WISE; os sensores chegam na hora em que mudam.
                Uma entrada "sem leitura" nunca foi enviada pelo WISE: confira a fiação e a configuração daquela entrada.
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
