// Editar um WISE cadastrado: IP e nome (opcional). O IP de um WISE em medição não pode mudar
// (a API recusa e a mensagem aparece aqui).
import { useState } from 'react'
import { dispositivoIotService, type WiseDto } from '../services/dispositivoIotService'
import { mensagemErro } from '../services/api'
import { ipValido } from '../components/iot/situacaoWise'
import { btnPrimary, btnSecondarySm } from '../styles/buttons'
import { inputMdFull, label } from '../styles/inputs'
import { modalOverlayNested, modalPanel, modalHeader, modalTitle, modalSubtitle, modalBody, modalFooter } from '../styles/modals'

interface Props {
  wise: WiseDto
  onFechar: () => void
  onSalvo: () => void
}

export default function WiseModal({ wise, onFechar, onSalvo }: Props) {
  const [ip, setIp] = useState(wise.enderecoIp)
  const [nome, setNome] = useState(wise.nome ?? '')
  const [salvando, setSalvando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  async function salvar() {
    setSalvando(true)
    setErro(null)
    try {
      await dispositivoIotService.editar(wise.id!, { enderecoIp: ip.trim(), nome: nome.trim() || null })
      onSalvo()
    } catch (e) {
      setErro(mensagemErro(e, 'Não foi possível salvar o WISE.'))
    } finally {
      setSalvando(false)
    }
  }

  return (
    <div className={modalOverlayNested}>
      <div className={`${modalPanel} w-[420px]`}>
        <div className={modalHeader}>
          <p className={modalTitle}>Editar WISE</p>
          <p className={modalSubtitle}>{wise.nome ? `${wise.nome} · ` : ''}{wise.enderecoIp}</p>
        </div>

        <div className={modalBody}>
          {erro && (
            <div className="bg-red-50 dark:bg-red-950 border border-red-200 dark:border-red-800 px-3 py-2 text-xs text-red-600 dark:text-red-400">
              {erro}
            </div>
          )}
          <div>
            <label className={label}>IP</label>
            <input value={ip} onChange={e => setIp(e.target.value)} className={inputMdFull} />
            {wise.medicao && (
              <p className="text-[10px] text-zinc-400 mt-1">Em medição na {wise.medicao.maquina}: o IP só pode mudar depois de finalizar.</p>
            )}
          </div>
          <div>
            <label className={label}>Nome (opcional)</label>
            <input value={nome} onChange={e => setNome(e.target.value)} maxLength={60} placeholder="ex.: WISE 03" className={inputMdFull} />
          </div>
        </div>

        <div className={modalFooter}>
          <button onClick={onFechar} disabled={salvando} className={btnSecondarySm}>Cancelar</button>
          <button onClick={salvar} disabled={salvando || !ipValido(ip)} className={btnPrimary}>
            {salvando ? 'Salvando...' : 'Salvar'}
          </button>
        </div>
      </div>
    </div>
  )
}
