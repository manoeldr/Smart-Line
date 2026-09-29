// WISE da medição, no Configurar medição (Semi Auto): o IP do WISE instalado na máquina e a
// situação dele em texto colorido — conectado (verde, pode iniciar), desconectado (amarelo) ou
// em uso em outra medição (vermelho). O WISE fica associado à máquina só enquanto a medição
// dura: ao finalizar, fica livre para outra. Sugere os WISE livres e conectados.
import type { WiseDto } from '../../services/dispositivoIotService'
import SituacaoWiseTexto from './SituacaoWiseTexto'
import { ipValido, situacaoDoIp } from './situacaoWise'
import { tempoDesde } from '../../utils/tempo'
import { inputMdFull, label } from '../../styles/inputs'

interface Props {
  ip: string
  onChangeIp: (ip: string) => void
  // Lista de WISE conhecidos (nula enquanto carrega) e erro da última consulta
  wises: WiseDto[] | null
  erroLista: string | null
  agora: Date
}

export default function WiseDaMedicao({ ip, onChangeIp, wises, erroLista, agora }: Props) {
  const digitado = ip.trim()
  const livres = (wises ?? []).filter(w => w.conectado && !w.medicao && w.enderecoIp !== digitado)

  function situacao() {
    if (!digitado)
      return <p className="text-zinc-400">Informe o IP fixo configurado no WISE instalado nesta máquina.</p>
    if (!ipValido(digitado))
      return <p className="text-red-600 dark:text-red-400">IP inválido. Use o formato 192.168.10.21.</p>
    if (!wises)
      return <p className="text-zinc-400">{erroLista ?? 'Consultando os WISE...'}</p>

    const { situacao, wise } = situacaoDoIp(digitado, wises)
    const detalhe =
      situacao === 'EmUso' && wise?.medicao
        ? `na medição da ${wise.medicao.maquina} (${wise.medicao.linha} · ${wise.medicao.cliente}), iniciada por ${wise.medicao.usuario}. ` +
          'Finalize aquela medição para usar este WISE aqui.'
        : situacao === 'Conectado'
          ? `última mensagem ${tempoDesde(wise?.ultimaMensagemUtc ?? null, agora)}${wise?.clientId ? ` · ${wise.clientId}` : ''}`
          : 'Não está conectado ao SmartLine. Verifique energia, rede e a configuração MQTT do WISE ' +
            '(o teste de ping fica em Configurações → Dispositivos IoT).'

    return (
      <>
        <p className="text-zinc-500">WISE {digitado} <SituacaoWiseTexto situacao={situacao} /></p>
        <p className="text-[10px] text-zinc-400 mt-0.5">{detalhe}</p>
      </>
    )
  }

  return (
    <div className="border border-zinc-200 dark:border-zinc-800 px-3 py-2.5 flex flex-col gap-2 text-xs">
      <div>
        <label className={label}>IP do WISE desta máquina</label>
        <input
          value={ip}
          onChange={e => onChangeIp(e.target.value)}
          placeholder="ex.: 192.168.10.21"
          className={inputMdFull}
        />
      </div>

      <div>{situacao()}</div>

      {livres.length > 0 && (
        <p className="text-[10px] text-zinc-400 flex flex-wrap items-center gap-1.5">
          WISE livres conectados:
          {livres.map(w => (
            <button
              key={w.enderecoIp}
              onClick={() => onChangeIp(w.enderecoIp)}
              title={w.clientId ?? undefined}
              className="px-1.5 py-0.5 border border-zinc-200 dark:border-zinc-700 text-zinc-600 dark:text-zinc-300 hover:bg-zinc-50 dark:hover:bg-zinc-800"
            >
              {w.enderecoIp}
            </button>
          ))}
        </p>
      )}

      <p className="text-[10px] text-zinc-400">
        O WISE fica associado a esta máquina até a medição ser finalizada; depois fica livre para outra máquina.
      </p>
    </div>
  )
}
