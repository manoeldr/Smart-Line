// Aba "Usuários" da tela de Configurações — acesso exclusivo do Administrador/Desenvolvedor.
// CRUD completo: nome, login, senha, nível de acesso e cliente vinculado (opcional).
// O perfil Desenvolvedor é reservado: o Administrador não vê os Desenvolvedores (o backend não os
// manda) nem pode dar esse nível a alguém; aqui a opção nem aparece para ele.
import { useEffect, useState } from 'react'
import { useParametroUrl } from '../../hooks/useParametroUrl'
import { useAuth } from '../../contexts/AuthContext'
import { mensagemErro } from '../../services/api'
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
  const { usuario } = useAuth()
  const niveis = usuario?.nivel === 'Desenvolvedor' ? NIVEIS : NIVEIS.filter(n => n !== 'Desenvolvedor')
  const [erro, setErro] = useState<string | null>(null)
  const [usuarios, setUsuarios] = useState<UsuarioConfDto[]>([])
  const [clientes, setClientes] = useState<ClienteConfDto[]>([])
  const [loading, setLoading] = useState(true)
  const [modalOpen, setModalOpen] = useState(false)
  const [editando, setEditando] = useState<UsuarioConfDto | null>(null)
  // Usuário em edição na URL (?editar=) para o F5 reabrir o modal.
  const [, setEditarId] = useParametroUrl('editar')
  const [form, setForm] = useState({ nome: '', login: '', senha: '', nivel: 'Auditor', clienteId: '', email: '' })
  const [salvando, setSalvando] = useState(false)

  // Confirmação de desativação (substitui o confirm() nativo do navegador)
  const [usuarioParaDeletar, setUsuarioParaDeletar] = useState<string | null>(null)

  useEffect(() => {
    carregar().then(lista => {
      const id = new URLSearchParams(window.location.search).get('editar')
      const u = id ? lista.find(x => x.id === id) : undefined
      if (u) abrirEditar(u)
      else if (id) setEditarId(null)
    })
    // Só ao abrir a aba: reabre o modal que estava aberto antes do F5.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  async function carregar() {
    setLoading(true)
    try {
      const [u, c] = await Promise.all([
        configuracaoService.getUsuarios(),
        configuracaoService.getClientes(),
      ])
      setUsuarios(u)
      setClientes(c)
      return u
    } finally { setLoading(false) }
  }

  function fecharModal() {
    setModalOpen(false)
    setEditarId(null)
  }

  function abrirNovo() {
    setEditando(null)
    setForm({ nome: '', login: '', senha: '', nivel: 'Auditor', clienteId: '', email: '' })
    setModalOpen(true)
  }

  function abrirEditar(u: UsuarioConfDto) {
    setEditando(u)
    setForm({ nome: u.nome, login: u.login, senha: '', nivel: u.nivel, clienteId: u.clienteId ?? '', email: u.email ?? '' })
    setModalOpen(true)
    setEditarId(u.id)
  }

  async function salvar() {
    setSalvando(true)
    setErro(null)
    try {
      const data = {
        nome: form.nome,
        login: form.login,
        nivel: form.nivel,
        clienteId: form.clienteId || null,
        email: form.email.trim() || null,
        // Ao editar, senha é opcional (só troca se preenchida); ao criar, é obrigatória
        ...(editando ? { senha: form.senha || undefined, ativo: editando.ativo } : { senha: form.senha }),
      }
      if (editando) {
        await configuracaoService.editarUsuario(editando.id, data)
      } else {
        await configuracaoService.criarUsuario(data)
      }
      fecharModal()
      await carregar()
    } catch (e) {
      setErro(mensagemErro(e, 'Não foi possível salvar o usuário.'))
    } finally { setSalvando(false) }
  }

  async function confirmarDeletar() {
    if (!usuarioParaDeletar) return
    try {
      await configuracaoService.deletarUsuario(usuarioParaDeletar)
    } catch (e) {
      setErro(mensagemErro(e, 'Não foi possível desativar o usuário.'))
    }
    setUsuarioParaDeletar(null)
    await carregar()
  }

  return (
    <div>
      {erro && (
        <div className="mb-3 bg-red-50 dark:bg-red-950 border border-red-200 dark:border-red-800 px-3 py-2 text-xs text-red-600 dark:text-red-400 flex items-center justify-between">
          <span>{erro}</span>
          <button onClick={() => setErro(null)} className="text-red-400 hover:text-red-600">fechar</button>
        </div>
      )}
      <div className="flex items-center justify-between mb-4">
        <p className="text-sm font-medium text-texto">Usuários</p>
        <button onClick={abrirNovo} className={`${btnPrimarySm} flex items-center gap-1.5`}>
          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
          Novo usuário
        </button>
      </div>

      {loading ? (
        <p className="text-xs text-texto-3">Carregando...</p>
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
              <p className="text-sm font-medium text-texto">
                {editando ? 'Editar usuário' : 'Novo usuário'}
              </p>
              <button onClick={fecharModal} className="text-texto-3 hover:text-texto">
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
              {form.nivel === 'Desenvolvedor' ? (
                <p className="text-[11px] text-texto-2">O Desenvolvedor não tem senha cadastrada: entra com a senha gerada para este computador (Desenvolvedor → Licença).</p>
              ) : (
                <div>
                  <label className={label}>{editando ? 'Nova senha (deixe em branco para manter)' : 'Senha'}</label>
                  <input type="password" value={form.senha} onChange={e => setForm(f => ({ ...f, senha: e.target.value }))} className={inputBase} />
                </div>
              )}
              <div>
                <label className={label}>E-mail (opcional)</label>
                <input type="email" value={form.email} onChange={e => setForm(f => ({ ...f, email: e.target.value }))} placeholder="Recebe cópia dos relatórios que enviar" className={inputBase} />
              </div>
              <div>
                <label className={label}>Nível</label>
                <select value={form.nivel} onChange={e => setForm(f => ({ ...f, nivel: e.target.value }))} className={inputBase}>
                  {niveis.map(n => <option key={n} value={n}>{n}</option>)}
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
              <button onClick={fecharModal} className={btnSecondarySm}>Cancelar</button>
              <button onClick={salvar} disabled={!form.nome || !form.login || (!editando && !form.senha && form.nivel !== 'Desenvolvedor') || salvando} className={btnPrimary}>
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