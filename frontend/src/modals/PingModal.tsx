// Teste de ping até um WISE, a partir do PC do SmartLine (4 pacotes, como o ping do Windows).
// Separa problema de rede (não responde) de configuração MQTT (responde, mas não conecta).
import { useEffect, useState } from 'react'
import { dispositivoIotService, type ResultadoPingDto } from '../services/dispositivoIotService'
import { mensagemErro } from '../services/api'
import { btnPrimarySm, btnSecondarySm } from '../styles/buttons'
import { modalOverlayNested, modalPanel, modalHeader, modalTitle, modalSubtitle, modalBody, modalFooter } from '../styles/modals'
import { table, tableHeadRow, tableHeadCell, tableBodyRow } from '../styles/tables'

interface Props {
  // IP a testar; nulo = fechado
  ip: string | null
  // Conexão MQTT do WISE agora (nula se não se sabe), para o diagnóstico
  conectado: boolean | null
  // Porta do broker, para orientar a configuração MQTT do WISE
  porta: number | null
  onFechar: () => void
}

export default function PingModal(props: Props) {
  // O conteúdo só existe aberto: cada abertura faz um teste novo.
  return props.ip ? <Conteudo {...props} ip={props.ip} /> : null
}

function Conteudo({ ip, conectado, porta, onFechar }: Props & { ip: string }) {
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

              <Diagnostico resultado={resultado} conectado={conectado} porta={porta} />
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

function Diagnostico({ resultado, conectado, porta }: { resultado: ResultadoPingDto; conectado: boolean | null; porta: number | null }) {
  const [cor, texto] =
    resultado.recebidos === 0
      ? ['text-red-600 dark:text-red-400',
         'O WISE não respondeu. Verifique a energia, o cabo e o switch, se o IP está certo e se o computador do SmartLine está na mesma rede do WISE.']
      : resultado.recebidos < resultado.enviados
        ? ['text-amber-600 dark:text-amber-400',
           'Houve perda de pacotes: a comunicação com o WISE está instável (cabo, switch ou rede sem fio).']
        : conectado === false
          ? ['text-amber-600 dark:text-amber-400',
             'A rede está ok, mas o WISE não está conectado ao SmartLine. Confira a configuração MQTT no WISE: ' +
             `servidor com o IP deste computador${porta ? ` e porta ${porta}` : ''}.`]
          : conectado
            ? ['text-green-600 dark:text-green-400', 'Rede ok e WISE conectado ao SmartLine.']
            : ['text-green-600 dark:text-green-400', 'Rede ok: o WISE responde.']

  return <p className={`text-xs ${cor}`}>{texto}</p>
}
