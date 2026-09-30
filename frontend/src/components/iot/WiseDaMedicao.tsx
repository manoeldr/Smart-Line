// WISE da medição, no Configurar medição (Semi Auto): escolha de um WISE da lista de
// cadastrados, cada um com a situação em texto colorido — conectado (verde, pode ser
// escolhido), desconectado (amarelo) ou em uso em outra medição (vermelho). O WISE fica
// associado à máquina só enquanto a medição dura; ao finalizar, fica livre para outra.
import { useAuth } from '../../contexts/AuthContext'
import type { WiseDto } from '../../services/dispositivoIotService'
import SituacaoWiseTexto from './SituacaoWiseTexto'
import { situacaoDoIp } from './situacaoWise'
import { tempoDesde } from '../../utils/tempo'
import { label } from '../../styles/inputs'

interface Props {
  // IP do WISE escolhido ('' = nenhum)
  ip: string
  onChangeIp: (ip: string) => void
  // Lista de WISE conhecidos (nula enquanto carrega) e erro da última consulta
  wises: WiseDto[] | null
  erroLista: string | null
  agora: Date
}

export default function WiseDaMedicao({ ip, onChangeIp, wises, erroLista, agora }: Props) {
  const { usuario } = useAuth()
  const podeCadastrar = ['Administrador', 'Desenvolvedor'].includes(usuario?.nivel ?? '')
  const cadastrados = (wises ?? []).filter(w => w.cadastrado)

  // Desconectado não tem detalhe: a palavra "desconectado" em amarelo já diz tudo.
  function detalhe(w: WiseDto) {
    if (w.medicao) return `em medição na ${w.medicao.maquina} (${w.medicao.linha} · ${w.medicao.cliente}), iniciada por ${w.medicao.usuario}`
    if (w.conectado) return `última mensagem ${tempoDesde(w.ultimaMensagemUtc, agora)}`
    return null
  }

  return (
    <div className="border border-zinc-200 dark:border-zinc-800 px-3 py-2.5 flex flex-col gap-2 text-xs">
      <label className={label}>WISE desta medição</label>

      {!wises ? (
        <p className="text-zinc-400">{erroLista ?? 'Consultando os WISE...'}</p>
      ) : cadastrados.length === 0 ? (
        <p className="text-zinc-400">
          Nenhum WISE cadastrado.{' '}
          {podeCadastrar ? 'Cadastre em Configurações → Dispositivos IoT.' : 'Peça a um Administrador para cadastrar em Configurações → Dispositivos IoT.'}
        </p>
      ) : (
        <div className="flex flex-col border border-zinc-200 dark:border-zinc-800 divide-y divide-zinc-200 dark:divide-zinc-800 max-h-48 overflow-y-auto">
          {cadastrados.map(w => {
            const { situacao } = situacaoDoIp(w.enderecoIp, cadastrados)
            const disponivel = situacao === 'Conectado'
            const escolhido = w.enderecoIp === ip
            return (
              <button
                key={w.enderecoIp}
                type="button"
                onClick={() => onChangeIp(escolhido ? '' : w.enderecoIp)}
                disabled={!disponivel && !escolhido}
                className={`flex items-center gap-3 px-3 py-2 text-left transition-colors disabled:cursor-not-allowed ${
                  escolhido
                    ? 'bg-blue-50 dark:bg-blue-950'
                    : disponivel ? 'hover:bg-zinc-50 dark:hover:bg-zinc-800' : 'opacity-70'
                }`}
              >
                <span className={`w-3 h-3 flex-shrink-0 rounded-full border ${escolhido ? 'border-blue-600 bg-blue-600' : 'border-zinc-300 dark:border-zinc-600'}`} />
                <span className="min-w-0 flex-1">
                  <span className="text-zinc-900 dark:text-zinc-100">
                    {w.nome ? `${w.nome} · ` : ''}{w.enderecoIp}
                  </span>{' '}
                  <SituacaoWiseTexto situacao={situacao} />
                  {detalhe(w) && <span className="block text-[10px] text-zinc-400 truncate">{detalhe(w)}</span>}
                </span>
              </button>
            )
          })}
        </div>
      )}

      <p className="text-[10px] text-zinc-400">
        Só dá para escolher um WISE conectado e livre. Ele fica associado a esta máquina até a medição ser finalizada.
      </p>
    </div>
  )
}
