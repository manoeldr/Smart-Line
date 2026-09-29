// Histórico de classificação de uma parada: o que o sensor disse ("Sistema") e cada troca
// manual, com quem trocou e quando.
import { useEffect, useState } from 'react'
import { paradaColetaService, type HistoricoClassificacaoDto } from '../services/paradaColetaService'
import { mensagemErro } from '../services/api'
import { dataHora } from '../utils/tempo'
import { btnSecondarySm } from '../styles/buttons'
import { modalOverlayNested, modalPanel, modalHeader, modalTitle, modalSubtitle, modalBody, modalFooter } from '../styles/modals'

interface Props {
  paradaId: string | null
  onFechar: () => void
}

export default function HistoricoParadaModal({ paradaId, onFechar }: Props) {
  return paradaId ? <Conteudo paradaId={paradaId} onFechar={onFechar} /> : null
}

function Conteudo({ paradaId, onFechar }: { paradaId: string; onFechar: () => void }) {
  const [historico, setHistorico] = useState<HistoricoClassificacaoDto[] | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  useEffect(() => {
    let ativo = true
    paradaColetaService.historico(paradaId)
      .then(h => { if (ativo) setHistorico(h) })
      .catch(e => { if (ativo) setErro(mensagemErro(e, 'Erro ao carregar o histórico.')) })
    return () => { ativo = false }
  }, [paradaId])

  return (
    <div className={modalOverlayNested}>
      <div className={`${modalPanel} w-[440px] max-h-[80vh]`}>
        <div className={modalHeader}>
          <p className={modalTitle}>Histórico do motivo</p>
          <p className={modalSubtitle}>Cada vez que o motivo desta parada foi definido ou trocado</p>
        </div>
        <div className={modalBody}>
          {erro ? (
            <p className="text-xs text-red-600 dark:text-red-400">{erro}</p>
          ) : !historico ? (
            <p className="text-xs text-zinc-400">Carregando...</p>
          ) : historico.length === 0 ? (
            <p className="text-xs text-zinc-400">Nenhuma classificação registrada: a parada ficou sem motivo desde o início.</p>
          ) : (
            <div className="flex flex-col">
              {historico.map((h, i) => (
                <div key={i} className="py-2 border-b border-zinc-100 dark:border-zinc-800 last:border-0 text-xs">
                  <p className="text-zinc-900 dark:text-zinc-100">
                    {h.motivoAnterior === null && h.usuarioId === null
                      ? <>Sensor classificou como <span className="font-medium">{h.motivoNovo ?? 'sem motivo'}</span></>
                      : <>{h.motivoAnterior ?? 'sem motivo'} {'->'} <span className="font-medium">{h.motivoNovo ?? 'sem motivo'}</span></>}
                  </p>
                  <p className="text-[10px] text-zinc-400 mt-0.5">{h.autor} · {dataHora(h.alteradoEm)}</p>
                </div>
              ))}
            </div>
          )}
        </div>
        <div className={modalFooter}>
          <button onClick={onFechar} className={btnSecondarySm}>Fechar</button>
        </div>
      </div>
    </div>
  )
}
