// Bloco da coleta Semi Automática no modal de detalhe da máquina: situação ao vivo, WISE,
// produção da sessão, parada em curso, sensores em texto e canais lidos. Atualiza a cada 5 s.
// Não mostra nada se a máquina não está em coleta automática. Finalizar: quem iniciou,
// Administrador ou Desenvolvedor (o backend confere de novo).
import { useEffect, useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { coletaIotService, type ColetaIotPainelDto } from '../../services/coletaIotService'
import { mensagemErro } from '../../services/api'
import ConfirmModal from '../ConfirmModal'
import SituacaoColetaTexto from './SituacaoColetaTexto'
import SituacaoWiseTexto from './SituacaoWiseTexto'
import { dataHora, tempoDesde } from '../../utils/tempo'
import { btnDangerSm } from '../../styles/buttons'

const INTERVALO_MS = 5000

interface Props {
  maquinaLinhaId: string
  onFinalizada: () => void
}

export default function PainelColetaIot({ maquinaLinhaId, onFinalizada }: Props) {
  const { usuario } = useAuth()
  const [painel, setPainel] = useState<ColetaIotPainelDto | null>(null)
  const [agora, setAgora] = useState(new Date())
  const [confirmando, setConfirmando] = useState(false)
  const [finalizando, setFinalizando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  useEffect(() => {
    let ativo = true
    async function atualizar() {
      try {
        const p = await coletaIotService.painelDaMaquina(maquinaLinhaId)
        if (ativo) setPainel(p)
      } catch {
        // 404 = máquina sem coleta automática: o bloco simplesmente não aparece
        if (ativo) setPainel(null)
      } finally {
        if (ativo) setAgora(new Date())
      }
    }
    atualizar()
    const id = setInterval(atualizar, INTERVALO_MS)
    return () => { ativo = false; clearInterval(id) }
  }, [maquinaLinhaId])

  if (!painel) return null

  const c = painel.coleta
  const nivel = usuario?.nivel ?? ''
  const podeFinalizar = nivel === 'Administrador' || nivel === 'Desenvolvedor' || (nivel === 'Auditor' && usuario?.id === c.usuarioId)
  const parada = c.paradaAberta

  async function finalizar() {
    setConfirmando(false)
    setFinalizando(true)
    setErro(null)
    try {
      await coletaIotService.finalizar(c.acompanhamentoId)
      onFinalizada()
    } catch (e) {
      setErro(mensagemErro(e, 'Não foi possível finalizar a coleta.'))
    } finally {
      setFinalizando(false)
    }
  }

  return (
    <div className="mx-5 mt-4 border border-zinc-200 dark:border-zinc-800 flex-shrink-0">
      {/* Cabeçalho do bloco */}
      <div className="px-3 py-2 bg-zinc-50 dark:bg-zinc-800/50 border-b border-zinc-200 dark:border-zinc-800 flex items-center justify-between gap-3">
        <p className="text-xs text-zinc-500">
          <span className="font-medium text-zinc-900 dark:text-zinc-100">Coleta Semi Automática</span>
          {' · '}<SituacaoColetaTexto situacao={painel.situacao} />
          {' · '}iniciada por {c.usuario} em {dataHora(c.iniciadoEm)}
        </p>
        {podeFinalizar && (
          <button onClick={() => setConfirmando(true)} disabled={finalizando} className={btnDangerSm}>
            {finalizando ? 'Finalizando...' : 'Finalizar coleta'}
          </button>
        )}
      </div>

      <div className="px-3 py-2.5 flex flex-col gap-2 text-xs">
        {erro && <p className="text-red-600 dark:text-red-400">{erro}</p>}

        {/* Números principais */}
        <div className="flex flex-wrap gap-x-6 gap-y-1 text-zinc-500">
          <p>
            WISE {c.enderecoIp ?? ''} <SituacaoWiseTexto situacao={painel.wiseConectado ? 'Conectado' : 'Desconectado'} />
            {painel.ultimaMensagem && <span className="text-zinc-400"> · última mensagem {tempoDesde(painel.ultimaMensagem, agora)}</span>}
          </p>
          <p>Produção da sessão: <span className="font-medium text-zinc-900 dark:text-zinc-100">{painel.producaoSessao.toLocaleString('pt-BR')}</span></p>
          <p>Rejeito: <span className="text-zinc-900 dark:text-zinc-100">{painel.refugoSessao.toLocaleString('pt-BR')}</span></p>
          {c.paradasNaoClassificadas > 0 && (
            <p className="text-amber-600 dark:text-amber-400">{c.paradasNaoClassificadas} {c.paradasNaoClassificadas === 1 ? 'parada sem motivo' : 'paradas sem motivo'}</p>
          )}
        </div>

        {/* Parada em curso / sem comunicação */}
        {parada && (
          <p className="text-red-600 dark:text-red-400">
            Parada desde {dataHora(parada.inicio)} ({tempoDesde(parada.inicio, agora).replace('há ', '')})
            {' · '}{parada.motivo ? `${parada.motivo} (${parada.tipo.toLowerCase()})` : 'sem motivo (conta como interna)'}
          </p>
        )}
        {c.semComunicacaoDesde && (
          <p className="text-zinc-500">Sem comunicação com o WISE desde {dataHora(c.semComunicacaoDesde)}.</p>
        )}

        {/* Sensores em texto */}
        {painel.sensores.length > 0 && (
          <div className="flex flex-wrap gap-1.5">
            {painel.sensores.map(s => (
              <span
                key={s.canal}
                title={s.nome}
                className={`text-[10px] px-1.5 py-0.5 border ${
                  s.emAlarme
                    ? 'border-amber-300 dark:border-amber-800 text-amber-700 dark:text-amber-400 bg-amber-50 dark:bg-amber-950'
                    : 'border-zinc-200 dark:border-zinc-700 text-zinc-600 dark:text-zinc-300'
                }`}
              >
                {s.canal} - {s.texto}
              </span>
            ))}
          </div>
        )}

        {/* Canais lidos */}
        <p className="text-[10px] text-zinc-400">
          Canais: {c.canais.map(x => `${x.canal}${x.multiplicador > 1 ? ` ×${x.multiplicador}` : ''}`).join(', ')}
          {' · '}parada após {c.tempoDeteccaoParadaSegundos} s sem produção
          {Object.keys(painel.contadores).length > 0 && (
            <> · contadores: {Object.entries(painel.contadores).map(([canal, v]) => `${canal}=${v.toLocaleString('pt-BR')}`).join(', ')}</>
          )}
        </p>
      </div>

      <ConfirmModal
        open={confirmando}
        titulo="Finalizar coleta"
        mensagem={`Finalizar a coleta Semi Automática da ${c.maquina}? A produção ainda não gravada é gravada antes, e a máquina fica sem coleta até alguém iniciar de novo.`}
        onConfirmar={finalizar}
        onCancelar={() => setConfirmando(false)}
      />
    </div>
  )
}
