// Modal "Enviar por e-mail" da tela de Relatórios: pede o e-mail do responsável do cliente
// (com os já usados nesse cliente como sugestão), mostra assunto e mensagem editáveis e, ao
// confirmar, o desktop grava o PDF e abre a mensagem no programa de e-mail com ele anexado.
import { useState } from 'react'
import { btnPrimary, btnSecondary } from '../styles/buttons'
import { inputMdFull, label } from '../styles/inputs'
import { modalOverlay, modalPanel, modalHeader, modalTitle, modalSubtitle, modalBody, modalFooter } from '../styles/modals'
import CampoComLista from '../components/CampoComLista'

interface Props {
  open: boolean
  assuntoInicial: string
  mensagemInicial: string
  emailsSugeridos: string[]
  onRemoverEmail: (email: string) => void
  onConfirmar: (dados: { para: string; assunto: string; mensagem: string }) => void
  onFechar: () => void
}

const EMAIL_VALIDO = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export default function EnviarRelatorioEmailModal({ open, assuntoInicial, mensagemInicial, emailsSugeridos, onRemoverEmail, onConfirmar, onFechar }: Props) {
  const [para, setPara] = useState('')
  const [assunto, setAssunto] = useState<string | null>(null)
  const [mensagem, setMensagem] = useState<string | null>(null)

  if (!open) return null

  const assuntoAtual = assunto ?? assuntoInicial
  const mensagemAtual = mensagem ?? mensagemInicial
  const emailOk = EMAIL_VALIDO.test(para.trim())

  function confirmar() {
    if (!emailOk) return
    onConfirmar({ para: para.trim(), assunto: assuntoAtual.trim(), mensagem: mensagemAtual })
  }

  return (
    <div className={modalOverlay}>
      <div className={`${modalPanel} w-[30rem] max-w-[95vw]`}>
        <div className={modalHeader}>
          <p className={modalTitle}>Enviar por e-mail</p>
          <p className={modalSubtitle}>A mensagem abre no seu programa de e-mail com o PDF anexado; você confere e envia.</p>
        </div>
        <div className={modalBody}>
          <div>
            <label className={label}>E-mail do responsável do cliente</label>
            <CampoComLista
              valor={para}
              onChange={setPara}
              sugestoes={emailsSugeridos}
              onRemoverSugestao={onRemoverEmail}
              placeholder="nome@empresa.com.br"
              obrigatorio
            />
          </div>
          <div>
            <label className={label}>Assunto</label>
            <input type="text" value={assuntoAtual} onChange={e => setAssunto(e.target.value)} className={inputMdFull} />
          </div>
          <div>
            <label className={label}>Mensagem</label>
            <textarea
              value={mensagemAtual}
              onChange={e => setMensagem(e.target.value)}
              rows={6}
              className="w-full px-3 py-2 border border-borda-forte bg-campo text-sm text-texto focus:outline-none focus:ring-2 focus:ring-accent-500 resize-y"
            />
          </div>
        </div>
        <div className={modalFooter}>
          <button onClick={onFechar} className={btnSecondary}>Cancelar</button>
          <button onClick={confirmar} disabled={!emailOk} className={btnPrimary}>Abrir e-mail</button>
        </div>
      </div>
    </div>
  )
}
