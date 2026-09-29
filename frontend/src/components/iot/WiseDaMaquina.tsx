// Quadro do WISE no Configurar medição (Semi Auto): situação em texto colorido e, para
// Administrador/Desenvolvedor, o cadastro do WISE ali mesmo — só o IP; máquina, linha e
// cliente já vêm da seleção feita na Medição. Sugere os IPs que estão publicando sem cadastro.
import { useEffect, useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { dispositivoIotService, type SituacaoWiseDto, type WiseDesconhecidoDto } from '../../services/dispositivoIotService'
import { mensagemErro } from '../../services/api'
import SituacaoWiseTexto from './SituacaoWiseTexto'
import { tempoDesde } from '../../utils/tempo'
import { btnPrimarySm, btnSecondarySm } from '../../styles/buttons'
import { inputBase } from '../../styles/inputs'

interface Props {
  maquinaLinhaId: string
  maquinaNome: string
  // Situação atual (nula enquanto carrega) e erro da última consulta
  wise: SituacaoWiseDto | null
  erroConsulta: string | null
  agora: Date
  // Cadastro ou troca de IP feita: quem usa consulta a situação de novo
  onAlterado: () => void
}

export default function WiseDaMaquina({ maquinaLinhaId, maquinaNome, wise, erroConsulta, agora, onAlterado }: Props) {
  const { usuario } = useAuth()
  const podeCadastrar = ['Administrador', 'Desenvolvedor'].includes(usuario?.nivel ?? '')

  const [ip, setIp] = useState('')
  const [editando, setEditando] = useState(false)
  const [salvando, setSalvando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [aguardando, setAguardando] = useState<WiseDesconhecidoDto[]>([])

  const precisaCadastro = wise?.situacao === 'NaoCadastrado'
  const formularioAberto = podeCadastrar && (precisaCadastro || editando)

  // IPs publicando sem cadastro: atalho para preencher o campo
  useEffect(() => {
    if (!formularioAberto) return
    let ativo = true
    dispositivoIotService.desconhecidos()
      .then(d => { if (ativo) setAguardando(d) })
      .catch(() => { /* só uma sugestão: sem ela, digita o IP */ })
    return () => { ativo = false }
  }, [formularioAberto])

  async function salvar() {
    setSalvando(true)
    setErro(null)
    try {
      const dados = { maquinaLinhaId, nome: wise?.nome ?? `WISE ${maquinaNome}`, enderecoIp: ip.trim(), ativo: true }
      // Já existe um WISE nesta máquina (inativo ou trocando o IP): atualiza em vez de criar outro
      if (wise?.dispositivoId) await dispositivoIotService.editar(wise.dispositivoId, dados)
      else await dispositivoIotService.criar(dados)
      setEditando(false)
      setIp('')
      onAlterado()
    } catch (e) {
      setErro(mensagemErro(e, 'Não foi possível cadastrar o WISE.'))
    } finally {
      setSalvando(false)
    }
  }

  return (
    <div className="border border-zinc-200 dark:border-zinc-800 px-3 py-2.5 flex flex-col gap-2 text-xs">
      {/* Situação */}
      {!wise ? (
        <p className="text-zinc-400">{erroConsulta ?? 'Consultando o WISE...'}</p>
      ) : (
        <div className="flex items-center justify-between gap-3">
          <div className="min-w-0">
            <p className="text-zinc-500">
              WISE {wise.enderecoIp && wise.situacao !== 'NaoCadastrado' ? `${wise.enderecoIp} ` : ''}
              <SituacaoWiseTexto situacao={wise.situacao} />
            </p>
            <p className="text-[10px] text-zinc-400 mt-0.5">
              {wise.situacao === 'NaoCadastrado'
                ? podeCadastrar
                  ? 'Informe o IP fixo configurado no WISE desta máquina.'
                  : 'Peça a um Administrador para cadastrar o WISE desta máquina.'
                : wise.situacao === 'Desconectado'
                  ? 'Verifique energia, rede, o IP e a configuração MQTT do WISE.'
                  : `última mensagem ${tempoDesde(wise.ultimaMensagemUtc, agora)}`}
            </p>
          </div>
          {podeCadastrar && !precisaCadastro && !editando && (
            <button onClick={() => { setEditando(true); setIp(wise.enderecoIp ?? '') }} className="text-[10px] text-blue-600 dark:text-blue-400 hover:underline flex-shrink-0">
              Alterar IP
            </button>
          )}
        </div>
      )}

      {/* Cadastro / troca de IP */}
      {wise && formularioAberto && (
        <div className="flex flex-col gap-1.5">
          <div className="flex items-center gap-2">
            <input
              value={ip}
              onChange={e => setIp(e.target.value)}
              placeholder="IP do WISE, ex.: 192.168.10.21"
              className={`${inputBase} flex-1`}
            />
            <button onClick={salvar} disabled={!ip.trim() || salvando} className={btnPrimarySm}>
              {salvando ? 'Salvando...' : precisaCadastro ? 'Cadastrar WISE' : 'Salvar IP'}
            </button>
            {editando && (
              <button onClick={() => { setEditando(false); setErro(null) }} disabled={salvando} className={btnSecondarySm}>Cancelar</button>
            )}
          </div>
          {aguardando.length > 0 && (
            <p className="text-[10px] text-zinc-400 flex flex-wrap items-center gap-1.5">
              Publicando sem cadastro:
              {aguardando.map(w => (
                <button key={w.enderecoIp} onClick={() => setIp(w.enderecoIp)} className="px-1.5 py-0.5 border border-zinc-200 dark:border-zinc-700 text-zinc-600 dark:text-zinc-300 hover:bg-zinc-50 dark:hover:bg-zinc-800">
                  {w.enderecoIp}
                </button>
              ))}
            </p>
          )}
          {erro && <p className="text-[10px] text-red-600 dark:text-red-400">{erro}</p>}
        </div>
      )}
    </div>
  )
}
