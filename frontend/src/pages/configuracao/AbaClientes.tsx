// Aba "Clientes" da tela de Configurações.
// Administrador/Desenvolvedor: CRUD completo de clientes.
// Auditor: visão restrita, só pode gerenciar as Linhas dentro do modal (não edita nome/estado nem cria/exclui clientes).
import { useEffect, useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { configuracaoService, type ClienteConfDto } from '../../services/configuracaoService'
import ConfiguracaoClienteModal from '../../modals/ConfiguracaoClienteModal'
import ConfirmModal from '../../components/ConfirmModal'
import { btnPrimarySm, btnSecondarySm, btnPrimary, btnIcon, btnIconDanger } from '../../styles/buttons'
import { inputBase, label } from '../../styles/inputs'
import { badgeStatus } from '../../styles/badges'
import { modalOverlay, modalContainerMd } from '../../styles/modals'
import { table, tableHeadRow, tableHeadCell, tableBodyRow, tableBodyCell, tableBodyCellMuted, tableActionsCell } from '../../styles/tables'

const ESTADOS_BR = ['AC','AL','AM','AP','BA','CE','DF','ES','GO','MA','MG','MS','MT','PA','PB','PE','PI','PR','RJ','RN','RO','RR','RS','SC','SE','SP','TO']

export default function AbaClientes() {
  const { usuario } = useAuth()
  const nivel = usuario?.nivel ?? ''
  const somenteLinhas = nivel === 'Auditor'

  const [clientes, setClientes] = useState<ClienteConfDto[]>([])
  const [loading, setLoading] = useState(true)

  // Modal simples de criação (só nome + estado — usado apenas pelo Administrador/Desenvolvedor)
  const [modalNovoOpen, setModalNovoOpen] = useState(false)
  const [form, setForm] = useState({ nome: '', estado: '' })
  const [salvando, setSalvando] = useState(false)

  // Modal completo de edição (dados + linhas + máquinas) — ConfiguracaoClienteModal
  const [modalEditarOpen, setModalEditarOpen] = useState(false)
  const [editando, setEditando] = useState<ClienteConfDto | null>(null)

  // Confirmação de desativação (substitui o confirm() nativo do navegador)
  const [clienteParaDeletar, setClienteParaDeletar] = useState<string | null>(null)

  useEffect(() => { carregar() }, [])

  async function carregar() {
    setLoading(true)
    try { setClientes(await configuracaoService.getClientes()) }
    finally { setLoading(false) }
  }

  function abrirNovo() {
    setForm({ nome: '', estado: '' })
    setModalNovoOpen(true)
  }

  function abrirEditar(c: ClienteConfDto) {
    setEditando(c)
    setModalEditarOpen(true)
  }

  async function salvarNovo() {
    setSalvando(true)
    try {
      await configuracaoService.criarCliente({ nome: form.nome, estado: form.estado || null })
      setModalNovoOpen(false)
      await carregar()
    } finally { setSalvando(false) }
  }

  async function confirmarDeletar() {
    if (!clienteParaDeletar) return
    await configuracaoService.deletarCliente(clienteParaDeletar)
    setClienteParaDeletar(null)
    await carregar()
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">
          {somenteLinhas ? 'Clientes — Linhas de produção' : 'Clientes'}
        </p>
        {!somenteLinhas && (
          <button onClick={abrirNovo} className={`${btnPrimarySm} flex items-center gap-1.5`}>
            <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
            Novo cliente
          </button>
        )}
      </div>

      {loading ? (
        <p className="text-xs text-zinc-400">Carregando...</p>
      ) : (
        <table className={table}>
          <thead>
            <tr className={tableHeadRow}>
              <th className={tableHeadCell}>Nome</th>
              <th className={tableHeadCell}>Estado</th>
              <th className={tableHeadCell}>Status</th>
              <th className="py-2" />
            </tr>
          </thead>
          <tbody>
            {clientes.map(c => (
              <tr key={c.id} className={tableBodyRow}>
                <td className={tableBodyCell}>{c.nome}</td>
                <td className={tableBodyCellMuted}>{c.estado ?? '—'}</td>
                <td className="py-2.5 pr-4">
                  <span className={badgeStatus(c.ativo)}>
                    {c.ativo ? 'ativo' : 'inativo'}
                  </span>
                </td>
                <td className={tableActionsCell}>
                  {/* Auditor vê um ícone de "olho" (visualizar/gerenciar linhas), Administrador/Desenvolvedor vê o lápis de edição completa */}
                  <button onClick={() => abrirEditar(c)} className={btnIcon}>
                    {somenteLinhas ? (
                      <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7z"/><circle cx="12" cy="12" r="3"/></svg>
                    ) : (
                      <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
                    )}
                  </button>
                  {!somenteLinhas && (
                    <button onClick={() => setClienteParaDeletar(c.id)} className={btnIconDanger}>
                      <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/></svg>
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {/* Modal de criação — só existe para Administrador/Desenvolvedor */}
      {!somenteLinhas && modalNovoOpen && (
        <div className={modalOverlay}>
          <div className={modalContainerMd}>
            <div className="flex items-center justify-between mb-4">
              <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">Novo cliente</p>
              <button onClick={() => setModalNovoOpen(false)} className="text-zinc-400 hover:text-zinc-900 dark:hover:text-zinc-100">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
              </button>
            </div>
            <div className="flex flex-col gap-3 mb-4">
              <div>
                <label className={label}>Nome</label>
                <input value={form.nome} onChange={e => setForm(f => ({ ...f, nome: e.target.value }))} className={inputBase} />
              </div>
              <div>
                <label className={label}>Estado</label>
                <select value={form.estado} onChange={e => setForm(f => ({ ...f, estado: e.target.value }))} className={inputBase}>
                  <option value="">Selecione</option>
                  {ESTADOS_BR.map(e => <option key={e} value={e}>{e}</option>)}
                </select>
              </div>
            </div>
            <div className="grid grid-cols-2 gap-2">
              <button onClick={() => setModalNovoOpen(false)} className={btnSecondarySm}>Cancelar</button>
              <button onClick={salvarNovo} disabled={!form.nome || salvando} className={btnPrimary}>
                {salvando ? 'Salvando...' : 'Salvar'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal de edição — mostra dados básicos (se não for Auditor) + gestão de linhas/máquinas (sempre) */}
      <ConfiguracaoClienteModal
        open={modalEditarOpen}
        cliente={editando}
        somenteLinhas={somenteLinhas}
        onFechar={() => setModalEditarOpen(false)}
        onSalvo={carregar}
      />

      {/* Confirmação de desativação */}
      <ConfirmModal
        open={clienteParaDeletar !== null}
        titulo="Desativar cliente"
        mensagem="Tem certeza que deseja desativar este cliente?"
        onConfirmar={confirmarDeletar}
        onCancelar={() => setClienteParaDeletar(null)}
      />
    </div>
  )
}