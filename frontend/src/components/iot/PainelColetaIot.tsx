// Bloco da coleta Semi Automática no modal de detalhe da máquina: situação ao vivo, WISE,
// produção da sessão, parada em curso e sensores em texto, em duas colunas (rótulo | valor).
// Atualiza a cada 5 s. Canais, Z e contadores brutos não aparecem aqui (Desenvolvedor → Coleta).
// Retrátil: fechado mostra só o cabeçalho (situação, quem iniciou, Finalizar); a escolha fica
// guardada neste navegador. Começa fechado.
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
const CHAVE_ABERTO = 'smartline.painelColetaIot.aberto'

function lerAberto() {
  try {
    return localStorage.getItem(CHAVE_ABERTO) === '1'
  } catch {
    return false
  }
}

function guardarAberto(aberto: boolean) {
  try {
    localStorage.setItem(CHAVE_ABERTO, aberto ? '1' : '0')
  } catch {
    // sem armazenamento: só não lembra na próxima vez
  }
}

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
  const [aberto, setAberto] = useState(lerAberto)

  function alternar() {
    setAberto(a => {
      guardarAberto(!a)
      return !a
    })
  }

  useEffect(() => {
    let ativo = true
    async function atualizar() {
      try {
        const p = await coletaIotService.painelDaMaquina(maquinaLinhaId)
        if (ativo) setPainel(p)
      } catch (e) {
        // 404 = máquina sem coleta automática: o bloco não aparece. Qualquer outra falha
        // (rede, backend ocupado) mantém o último painel, em vez de sumir e voltar.
        if (ativo && e instanceof Error && e.message === 'Erro 404') setPainel(null)
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
    <div className="mx-5 mt-4 border border-borda flex-shrink-0">
      {/* Cabeçalho do bloco */}
      <div className={`px-3 py-2 bg-superficie-2 flex items-center justify-between gap-3 ${aberto ? 'border-b border-borda' : ''}`}>
        <button
          type="button"
          onClick={alternar}
          aria-expanded={aberto}
          title={aberto ? 'Recolher' : 'Ver detalhes da coleta'}
          className="flex items-center gap-2 text-left text-xs text-texto-2 min-w-0"
        >
          <svg
            width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"
            className={`flex-shrink-0 transition-transform ${aberto ? 'rotate-90' : ''}`}
          >
            <polyline points="9 18 15 12 9 6" />
          </svg>
          <span>
            <span className="font-medium text-texto">Coleta Semi Automática</span>
            {' · '}<SituacaoColetaTexto situacao={painel.situacao} />
            {' · '}iniciada por {c.usuario} em {dataHora(c.iniciadoEm)}
          </span>
        </button>
        {podeFinalizar && (
          <button onClick={() => setConfirmando(true)} disabled={finalizando} className={btnDangerSm}>
            {finalizando ? 'Finalizando...' : 'Finalizar coleta'}
          </button>
        )}
      </div>

      {erro && <p className="px-3 py-2 text-xs text-red-600 dark:text-red-400">{erro}</p>}

      {aberto && (
      <div className="px-3 py-2.5 text-xs">
        {/* Duas colunas: rótulo | valor */}
        <div className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 items-baseline">
          <span className="text-texto-3">WISE</span>
          <span className="text-texto-2">
            {c.enderecoIp ?? ''} <SituacaoWiseTexto situacao={painel.wiseConectado ? 'Conectado' : 'Desconectado'} />
            {painel.ultimaMensagem && <span className="text-texto-3"> · última mensagem {tempoDesde(painel.ultimaMensagem, agora)}</span>}
          </span>

          <span className="text-texto-3">Produção</span>
          <span className="font-medium text-texto">{painel.producaoSessao.toLocaleString('pt-BR')}</span>

          <span className="text-texto-3">Refugo</span>
          <span className="text-texto">{painel.refugoSessao.toLocaleString('pt-BR')}</span>

          {parada && (
            <>
              <span className="text-texto-3">Parada</span>
              <span className="text-red-600 dark:text-red-400">
                desde {dataHora(parada.inicio)} ({tempoDesde(parada.inicio, agora).replace('há ', '')})
                {' · '}{parada.motivo ? `${parada.motivo} (${parada.tipo.toLowerCase()})` : 'sem classificação (conta como interna)'}
              </span>
            </>
          )}

          {c.semComunicacaoDesde && (
            <>
              <span className="text-texto-3">Comunicação</span>
              <span className="text-texto-2">sem comunicação com o WISE desde {dataHora(c.semComunicacaoDesde)}</span>
            </>
          )}

          {c.paradasNaoClassificadas > 0 && (
            <>
              <span className="text-texto-3">Sem classificação</span>
              <span className="text-amber-600 dark:text-amber-400">
                {c.paradasNaoClassificadas} {c.paradasNaoClassificadas === 1 ? 'parada' : 'paradas'}
              </span>
            </>
          )}

          {painel.sensores.length > 0 && (
            <>
              <span className="text-texto-3">Sensores</span>
              <span className="flex flex-wrap gap-1.5">
                {painel.sensores.map(s => (
                  <span
                    key={s.canal}
                    title={s.nome}
                    className={`text-[10px] px-1.5 py-0.5 border ${
                      s.emAlarme
                        ? 'border-amber-300 dark:border-amber-800 text-amber-700 dark:text-amber-400 bg-amber-50 dark:bg-amber-950'
                        : 'border-borda-forte text-texto-2'
                    }`}
                  >
                    {s.canal} - {s.texto}
                  </span>
                ))}
              </span>
            </>
          )}
        </div>
      </div>
      )}

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
