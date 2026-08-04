// Aba "Máquinas" da tela de Configurações.
// Cadastro global de máquinas (não vinculadas a cliente/linha aqui).
// Ao clicar em editar, abre o ConfiguracaoMaquinaModal com abas de Manual/Semi/Automático,
// onde ficam os campos de coleta e motivos de parada daquela máquina.
import { useEffect, useState } from 'react'
import { configuracaoService, type MaquinaConfDto } from '../../services/configuracaoService'
import ConfiguracaoMaquinaModal from '../../modals/ConfiguracaoMaquinaModal'
import ConfirmModal from '../../components/ConfirmModal'
import { btnPrimarySm, btnIcon, btnIconDanger } from '../../styles/buttons'
import { badgeStatus } from '../../styles/badges'
import { table, tableHeadRow, tableHeadCell, tableBodyRow, tableBodyCell, tableBodyCellMuted, tableActionsCell } from '../../styles/tables'

export default function AbaMaquinas() {
  const [maquinas, setMaquinas] = useState<MaquinaConfDto[]>([])
  const [loading, setLoading] = useState(true)
  const [modalOpen, setModalOpen] = useState(false)
  const [editando, setEditando] = useState<MaquinaConfDto | null>(null)

  // Confirmação de desativação (substitui o confirm() nativo do navegador)
  const [maquinaParaDeletar, setMaquinaParaDeletar] = useState<string | null>(null)

  useEffect(() => { carregar() }, [])

  async function carregar() {
    setLoading(true)
    try { setMaquinas(await configuracaoService.getMaquinas()) }
    finally { setLoading(false) }
  }

  function abrirNovo() {
    setEditando(null)
    setModalOpen(true)
  }

  function abrirEditar(m: MaquinaConfDto) {
    setEditando(m)
    setModalOpen(true)
  }

  async function confirmarDeletar() {
    if (!maquinaParaDeletar) return
    await configuracaoService.deletarMaquina(maquinaParaDeletar)
    setMaquinaParaDeletar(null)
    await carregar()
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">Máquinas</p>
        <button onClick={abrirNovo} className={`${btnPrimarySm} flex items-center gap-1.5`}>
          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
          Nova máquina
        </button>
      </div>

      {loading ? (
        <p className="text-xs text-zinc-400">Carregando...</p>
      ) : (
        <table className={table}>
          <thead>
            <tr className={tableHeadRow}>
              <th className={tableHeadCell}>Nome</th>
              <th className={tableHeadCell}>Descrição</th>
              <th className={tableHeadCell}>Status</th>
              <th className="py-2" />
            </tr>
          </thead>
          <tbody>
            {maquinas.map(m => (
              <tr key={m.id} className={tableBodyRow}>
                <td className={tableBodyCell}>{m.nome}</td>
                <td className={tableBodyCellMuted}>{m.descricao ?? '—'}</td>
                <td className="py-2.5 pr-4">
                  <span className={badgeStatus(m.ativo)}>
                    {m.ativo ? 'ativa' : 'inativa'}
                  </span>
                </td>
                <td className={tableActionsCell}>
                  <button onClick={() => abrirEditar(m)} className={btnIcon}>
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
                  </button>
                  <button onClick={() => setMaquinaParaDeletar(m.id)} className={btnIconDanger}>
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/></svg>
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <ConfiguracaoMaquinaModal
        open={modalOpen}
        maquina={editando}
        onFechar={() => setModalOpen(false)}
        onSalvo={carregar}
      />

      {/* Confirmação de desativação */}
      <ConfirmModal
        open={maquinaParaDeletar !== null}
        titulo="Desativar máquina"
        mensagem="Tem certeza que deseja desativar esta máquina?"
        onConfirmar={confirmarDeletar}
        onCancelar={() => setMaquinaParaDeletar(null)}
      />
    </div>
  )
}