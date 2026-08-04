// Aba "Usuários" da tela de Configurações — acesso exclusivo do Administrador/Desenvolvedor.
// CRUD completo: nome, login, senha, nível de acesso e cliente vinculado (opcional).
import { useEffect, useState } from 'react'
import { configuracaoService, type UsuarioConfDto } from '../../services/configuracaoService'
import type { ClienteConfDto } from '../../services/configuracaoService'
import ConfirmModal from '../../components/ConfirmModal'
import { btnPrimarySm, btnSecondarySm, btnPrimary, btnIcon, btnIconDanger } from '../../styles/buttons'
import { inputBase, label } from '../../styles/inputs'
import { badgeStatus } from '../../styles/badges'
import { modalOverlay, modalContainerMd } from '../../styles/modals'
import { table, tableHeadRow, tableHeadCell, tableBodyRow, tableBodyCell, tableBodyCellMuted, tableActionsCell } from '../../styles/tables'

const NIVEIS = ['Administrador', 'Auditor', 'Cliente', 'Desenvolvedor']

export default function AbaUsuarios() {
  const [usuarios, setUsuarios] = useState<UsuarioConfDto[]>([])
  const [clientes, setClientes] = useState<ClienteConfDto[]>([])
  const [loading, setLoading] = useState(true)
  const [modalOpen, setModalOpen] = useState(false)
  const [editando, setEditando] = useState<UsuarioConfDto | null>(null)
  const [form, setForm] = useState({ nome: '', login: '', senha: '', nivel: 'Auditor', clienteId: '' })
  const [salvando, setSalvando] = useState(false)

  // Confirmação de desativação (substitui o confirm() nativo do navegador)
  const [usuarioParaDeletar, setUsuarioParaDeletar] = useState<string | null>(null)

  useEffect(() => { carregar() }, [])

  async function carregar() {
    setLoading(true)
    try {
      const [u, c] = await Promise.all([
        configuracaoService.getUsuarios(),
        configuracaoService.getClientes(),
      ])
      setUsuarios(u)
      setClientes(c)
    } finally { setLoading(false) }
  }

  function abrirNovo() {
    setEditando(null)
    setForm({ nome: '', login: '', senha: '', nivel: 'Auditor', clienteId: '' })
    setModalOpen(true)
  }

  function abrirEditar(u: UsuarioConfDto) {
    setEditando(u)
    setForm({ nome: u.nome, login: u.login, senha: '', nivel: u.nivel, clienteId: u.clienteId ?? '' })
    setModalOpen(true)
  }

  async function salvar() {
    setSalvando(true)
    try {
      const data = {
        nome: form.nome,
        login: form.login,
        nivel: form.nivel,
        clienteId: form.clienteId || null,
        // Ao editar, senha é opcional (só troca se preenchida); ao criar, é obrigatória
        ...(editando ? { senha: form.senha || undefined, ativo: editando.ativo } : { senha: form.senha }),
      }
      if (editando) {
        await configuracaoService.editarUsuario(editando.id, data)
      } else {
        await configuracaoService.criarUsuario(data)
      }
      setModalOpen(false)
      await carregar()
    } finally { setSalvando(false) }
  }

  async function confirmarDeletar() {
    if (!usuarioParaDeletar) return
    await configuracaoService.deletarUsuario(usuarioParaDeletar)
    setUsuarioParaDeletar(null)
    await carregar()
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">Usuários</p>
        <button onClick={abrirNovo} className={`${btnPrimarySm} flex items-center gap-1.5`}>
          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
          Novo usuário
        </button>
      </div>

      {loading ? (
        <p className="text-xs text-zinc-400">Carregando...</p>
      ) : (
        <table className={table}>
          <thead>
            <tr className={tableHeadRow}>
              <th className={tableHeadCell}>Nome</th>
              <th className={tableHeadCell}>Login</th>
              <th className={tableHeadCell}>Nível</th>
              <th className={tableHeadCell}>Cliente</th>
              <th className={tableHeadCell}>Status</th>
              <th className="py-2" />
            </tr>
          </thead>
          <tbody>
            {usuarios.map(u => (
              <tr key={u.id} className={tableBodyRow}>
                <td className={tableBodyCell}>{u.nome}</td>
                <td className={tableBodyCellMuted}>{u.login}</td>
                <td className={tableBodyCellMuted}>{u.nivel}</td>
                <td className={tableBodyCellMuted}>{u.clienteNome ?? '—'}</td>
                <td className="py-2.5 pr-4">
                  <span className={badgeStatus(u.ativo)}>
                    {u.ativo ? 'ativo' : 'inativo'}
                  </span>
                </td>
                <td className={tableActionsCell}>
                  <button onClick={() => abrirEditar(u)} className={btnIcon}>
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
                  </button>
                  <button onClick={() => setUsuarioParaDeletar(u.id)} className={btnIconDanger}>
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/></svg>
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {modalOpen && (
        <div className={modalOverlay}>
          <div className={modalContainerMd}>
            <div className="flex items-center justify-between mb-4">
              <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">
                {editando ? 'Editar usuário' : 'Novo usuário'}
              </p>
              <button onClick={() => setModalOpen(false)} className="text-zinc-400 hover:text-zinc-900 dark:hover:text-zinc-100">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
              </button>
            </div>
            <div className="flex flex-col gap-3 mb-4">
              <div>
                <label className={label}>Nome</label>
                <input value={form.nome} onChange={e => setForm(f => ({ ...f, nome: e.target.value }))} className={inputBase} />
              </div>
              <div>
                <label className={label}>Login</label>
                <input value={form.login} onChange={e => setForm(f => ({ ...f, login: e.target.value }))} className={inputBase} />
              </div>
              <div>
                <label className={label}>{editando ? 'Nova senha (deixe em branco para manter)' : 'Senha'}</label>
                <input type="password" value={form.senha} onChange={e => setForm(f => ({ ...f, senha: e.target.value }))} className={inputBase} />
              </div>
              <div>
                <label className={label}>Nível</label>
                <select value={form.nivel} onChange={e => setForm(f => ({ ...f, nivel: e.target.value }))} className={inputBase}>
                  {NIVEIS.map(n => <option key={n} value={n}>{n}</option>)}
                </select>
              </div>
              <div>
                <label className={label}>Cliente (opcional)</label>
                <select value={form.clienteId} onChange={e => setForm(f => ({ ...f, clienteId: e.target.value }))} className={inputBase}>
                  <option value="">Nenhum</option>
                  {clientes.filter(c => c.ativo).map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
                </select>
              </div>
            </div>
            <div className="grid grid-cols-2 gap-2">
              <button onClick={() => setModalOpen(false)} className={btnSecondarySm}>Cancelar</button>
              <button onClick={salvar} disabled={!form.nome || !form.login || (!editando && !form.senha) || salvando} className={btnPrimary}>
                {salvando ? 'Salvando...' : 'Salvar'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Confirmação de desativação */}
      <ConfirmModal
        open={usuarioParaDeletar !== null}
        titulo="Desativar usuário"
        mensagem="Tem certeza que deseja desativar este usuário?"
        onConfirmar={confirmarDeletar}
        onCancelar={() => setUsuarioParaDeletar(null)}
      />
    </div>
  )
}