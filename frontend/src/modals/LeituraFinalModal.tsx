// Modal de leitura final — obrigatório ao clicar em "Finalizar medição".
// Pede a produção final (só se a máquina medir produção) e o valor final de cada campo extra
// que estava sendo coletado na sessão.
import { useState } from 'react'
import type { CampoMaquinaDto } from '../services/configuracaoService'
import { btnPrimary, btnSecondarySm } from '../styles/buttons'
import { inputMdFull, label } from '../styles/inputs'
import { modalOverlayDark, modalPanel, modalHeader, modalTitle, modalSubtitle, modalBody, modalFooter } from '../styles/modals'

interface Props {
  open: boolean
  camposExtras: CampoMaquinaDto[]
  medeProducao?: boolean
  onConfirmar: (producaoFinal: number, extras: { campoMaquinaId: string; valor: number }[]) => void
  onCancelar: () => void
  salvando?: boolean
}

export default function LeituraFinalModal({ open, camposExtras, medeProducao = true, onConfirmar, onCancelar, salvando }: Props) {
  const [producaoFinal, setProducaoFinal] = useState('')
  const [valoresExtras, setValoresExtras] = useState<Record<string, string>>({})

  if (!open) return null

  function handleExtraChange(campoId: string, valor: string) {
    setValoresExtras(prev => ({ ...prev, [campoId]: valor }))
  }

  function handleConfirmar() {
    const producao = medeProducao ? Number(producaoFinal) : 0
    if (medeProducao && (isNaN(producao) || producaoFinal === '')) return

    const extras = camposExtras.map(c => ({
      campoMaquinaId: c.id,
      valor: Number(valoresExtras[c.id]) || 0,
    }))

    onConfirmar(producao, extras)
  }

  const podeConfirmar = medeProducao
    ? producaoFinal !== '' && !isNaN(Number(producaoFinal))
    : true

  return (
    <div className={modalOverlayDark}>
      <div className={`${modalPanel} w-96 max-h-[90vh]`}>

        <div className={`${modalHeader} flex items-start justify-between`}>
          <div>
            <p className={modalTitle}>Leitura final</p>
            <p className={modalSubtitle}>Informe os valores finais antes de encerrar</p>
          </div>
          <button onClick={onCancelar} disabled={salvando} className="text-zinc-400 hover:text-zinc-900 dark:hover:text-zinc-100 disabled:opacity-40">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
          </button>
        </div>

        <div className={modalBody}>
          {medeProducao && (
            <div>
              <label className={label}>Produção final</label>
              <input
                type="number" min="0" autoFocus
                value={producaoFinal}
                onChange={e => setProducaoFinal(e.target.value)}
                placeholder="Leitura final do contador"
                className={inputMdFull}
              />
            </div>
          )}

          {camposExtras.map(c => (
            <div key={c.id}>
              <label className={label}>
                {c.nome} final {c.unidade && `(${c.unidade})`}
              </label>
              <input
                type="number"
                value={valoresExtras[c.id] ?? ''}
                onChange={e => handleExtraChange(c.id, e.target.value)}
                placeholder="Leitura final"
                className={inputMdFull}
              />
            </div>
          ))}
        </div>

        <div className={modalFooter}>
          <button onClick={onCancelar} disabled={salvando} className={btnSecondarySm}>
            Cancelar
          </button>
          <button onClick={handleConfirmar} disabled={!podeConfirmar || salvando} className={btnPrimary}>
            {salvando ? 'Finalizando...' : 'Confirmar e finalizar'}
          </button>
        </div>
      </div>
    </div>
  )
}