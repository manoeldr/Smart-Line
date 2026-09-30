// Teste de ping até um WISE, a partir do PC do SmartLine (4 pacotes, como o ping do Windows).
// Mostra "WISE conectado" se ele respondeu e "WISE desconectado" se não respondeu.
import { useEffect, useState } from 'react'
import { dispositivoIotService, type ResultadoPingDto } from '../services/dispositivoIotService'
import { mensagemErro } from '../services/api'
import { btnPrimarySm, btnSecondarySm } from '../styles/buttons'
import { modalOverlayNested, modalPanel, modalHeader, modalTitle, modalSubtitle, modalBody, modalFooter } from '../styles/modals'
import { table, tableHeadRow, tableHeadCell, tableBodyRow } from '../styles/tables'

interface Props {
  // IP a testar; nulo = fechado
  ip: string | null
  onFechar: () => void
}

export default function PingModal(props: Props) {
  // O conteúdo só existe aberto: cada abertura faz um teste novo.
  return props.ip ? <Conteudo {...props} ip={props.ip} /> : null
}

function Conteudo({ ip, onFechar }: Props & { ip: string }) {
  const [resultado, setResultado] = useState<ResultadoPingDto | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [tentativa, setTentativa] = useState(0)

  useEffect(() => {
    let ativo = true
    async function pingar() {
      try {
        const r = await dispositivoIotService.ping(ip)
        if (ativo) setResultado(r)
      } catch (e) {
        if (ativo) setErro(mensagemErro(e, 'Não foi possível fazer o teste de ping.'))
      }
    }
    pingar()
    return () => { ativo = false }
  }, [ip, tentativa])

  function repetir() {
    setResultado(null)
    setErro(null)
    setTentativa(t => t + 1)
  }

  const testando = !resultado && !erro

  return (
    <div className={modalOverlayNested}>
      <div className={`${modalPanel} w-[480px] max-h-[90vh]`}>
        <div className={modalHeader}>
          <p className={modalTitle}>Teste de ping</p>
          <p className={modalSubtitle}>WISE {ip} · a partir do computador do SmartLine</p>
        </div>

        <div className={modalBody}>
          {erro && (
            <div className="bg-red-50 dark:bg-red-950 border border-red-200 dark:border-red-800 px-3 py-2 text-xs text-red-600 dark:text-red-400">
              {erro}
            </div>
          )}

          {testando && <p className="text-xs text-zinc-400">Enviando 4 pacotes para {ip}...</p>}

          {resultado && (
            <>
              <table className={table}>
                <thead>
                  <tr className={tableHeadRow}>
                    <th className={tableHeadCell}>#</th>
                    <th className={tableHeadCell}>Resposta</th>
                    <th className={tableHeadCell}>Tempo</th>
                  </tr>
                </thead>
                <tbody>
                  {resultado.respostas.map(r => (
                    <tr key={r.sequencia} className={tableBodyRow}>
                      <td className="py-2 pr-4 text-zinc-500">{r.sequencia}</td>
                      <td className={`py-2 pr-4 ${r.respondeu ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`}>{r.situacao}</td>
                      <td className="py-2 pr-4 text-zinc-900 dark:text-zinc-100">{r.tempoMs !== null ? `${r.tempoMs} ms` : '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>

              <p className="text-xs text-zinc-500">
                Enviados {resultado.enviados}, recebidos {resultado.recebidos}, perda de {resultado.perdaPercentual}%
                {resultado.tempoMedioMs !== null && (
                  <> · mínimo {resultado.tempoMinimoMs} ms, médio {resultado.tempoMedioMs} ms, máximo {resultado.tempoMaximoMs} ms</>
                )}
              </p>

              <Diagnostico resultado={resultado} />
            </>
          )}
        </div>

        <div className={modalFooter}>
          <button onClick={onFechar} className={btnSecondarySm}>Fechar</button>
          <button onClick={repetir} disabled={testando} className={btnPrimarySm}>
            {testando ? 'Testando...' : 'Testar de novo'}
          </button>
        </div>
      </div>
    </div>
  )
}

// Resultado em uma palavra: respondeu ao ping = conectado; não respondeu = desconectado.
function Diagnostico({ resultado }: { resultado: ResultadoPingDto }) {
  return resultado.recebidos > 0
    ? <p className="text-xs font-medium text-green-600 dark:text-green-400">WISE conectado</p>
    : <p className="text-xs font-medium text-red-600 dark:text-red-400">WISE desconectado</p>
}
