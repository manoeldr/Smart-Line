// Modal de confirmação reutilizável — substitui o confirm() nativo do navegador por algo
// visualmente consistente com o resto do sistema.
import { btnDanger, btnSecondarySm } from '../styles/buttons'
import { modalOverlayNested, modalContainerSm, modalTitle } from '../styles/modals'

interface Props {
  open: boolean
  titulo?: string
  mensagem: string
  onConfirmar: () => void
  onCancelar: () => void
}

export default function ConfirmModal({ open, titulo = 'Confirmar ação', mensagem, onConfirmar, onCancelar }: Props) {
  if (!open) return null

  return (
    <div className={modalOverlayNested}>
      <div className={modalContainerSm}>
        <p className={`${modalTitle} mb-2`}>{titulo}</p>
        <p className="text-xs text-zinc-500 mb-4">{mensagem}</p>
        <div className="grid grid-cols-2 gap-2">
          <button onClick={onCancelar} className={btnSecondarySm}>Cancelar</button>
          <button onClick={onConfirmar} className={btnDanger}>Confirmar</button>
        </div>
      </div>
    </div>
  )
}