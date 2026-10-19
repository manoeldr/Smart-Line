import { useEffect, useState } from 'react'
import { configuracaoService, type LinhaConfDto, type ClienteConfDto } from '../../services/configuracaoService'
import { table, tableHeadRow, tableHeadCell, tableBodyRow, tableBodyCell, tableBodyCellMuted, tableActionsCell } from '../../styles/tables'

export default function AbaLinhas() {
  const [linhas, setLinhas] = useState<LinhaConfDto[]>([])
  const [clientes, setClientes] = useState<ClienteConfDto[]>([])
  const [loading, setLoading] = useState(true)
  const [modalOpen, setModalOpen] = useState(false)
  const [editando, setEditando] = useState<LinhaConfDto | null>(null)
  const [form, setForm] = useState({ nome: '', clienteId: '' })
  const [salvando, setSalvando] = useState(false)

  useEffect(() => { carregar() }, [])

  async function carregar() {
    setLoading(true)
    try {
      const [l, c] = await Promise.all([
        configuracaoService.getLinhas(),
        configuracaoService.getClientes(),
      ])
      setLinhas(l)
      setClientes(c)
    } finally { setLoading(false) }
  }

  function abrirNovo() {
    setEditando(null)
    setForm({ nome: '', clienteId: clientes[0]?.id ?? '' })
    setModalOpen(true)
  }

  function abrirEditar(l: LinhaConfDto) {
    setEditando(l)
    setForm({ nome: l.nome, clienteId: l.clienteId })
    setModalOpen(true)
  }

  async function salvar() {
    setSalvando(true)
    try {
      if (editando) {
        await configuracaoService.editarLinha(editando.id, { nome: form.nome, ativo: editando.ativo })
      } else {
        await configuracaoService.criarLinha({ nome: form.nome, clienteId: form.clienteId })
      }
      setModalOpen(false)
      await carregar()
    } finally { setSalvando(false) }
  }

  async function deletar(id: string) {
    if (!confirm('Desativar esta linha?')) return
    await configuracaoService.deletarLinha(id)
    await carregar()
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <p className="text-sm font-medium text-texto">Linhas</p>
        <button onClick={abrirNovo} className="h-8 px-3 bg-accent-600 hover:bg-accent-700 text-accent-fg text-xs font-medium flex items-center gap-1.5">
          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
          Nova linha
        </button>
      </div>

      {loading ? (
        <p className="text-xs text-texto-3">Carregando...</p>
      ) : (
        <table className={table}>
          <thead>
            <tr className={tableHeadRow}>
              <th className={tableHeadCell}>Nome</th>
              <th className={tableHeadCell}>Cliente</th>
              <th className={tableHeadCell}>Status</th>
              <th className="py-2" />
            </tr>
          </thead>
          <tbody>
            {linhas.map(l => (
              <tr key={l.id} className={tableBodyRow}>
                <td className={tableBodyCell}>{l.nome}</td>
                <td className={tableBodyCellMuted}>{l.clienteNome}</td>
                <td className="py-2.5 pl-2 pr-4">
                  <span className={`px-1.5 py-0.5 text-[10px] ${l.ativo ? 'bg-green-50 dark:bg-green-950 text-green-700 dark:text-green-400' : 'bg-superficie-2 text-texto-3'}`}>
                    {l.ativo ? 'ativa' : 'inativa'}
                  </span>
                </td>
                <td className={tableActionsCell}>
                  <button onClick={() => abrirEditar(l)} className="text-texto-3 hover:text-texto">
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
                  </button>
                  <button onClick={() => deletar(l.id)} className="text-texto-3 hover:text-red-500">
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/></svg>
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {modalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-superficie border border-borda w-80 p-5">
            <p className="text-sm font-medium text-texto mb-4">
              {editando ? 'Editar linha' : 'Nova linha'}
            </p>
            <div className="flex flex-col gap-3 mb-4">
              <div>
                <label className="text-xs text-texto-2 mb-1 block">Nome</label>
                <input value={form.nome} onChange={e => setForm(f => ({ ...f, nome: e.target.value }))}
                  className="w-full h-8 px-2 text-xs border border-borda-forte bg-campo text-texto focus:outline-none focus:ring-1 focus:ring-accent-500" />
              </div>
              {!editando && (
                <div>
                  <label className="text-xs text-texto-2 mb-1 block">Cliente</label>
                  <select value={form.clienteId} onChange={e => setForm(f => ({ ...f, clienteId: e.target.value }))}
                    className="w-full h-8 px-2 text-xs border border-borda-forte bg-campo text-texto focus:outline-none focus:ring-1 focus:ring-accent-500">
                    {clientes.filter(c => c.ativo).map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
                  </select>
                </div>
              )}
            </div>
            <div className="grid grid-cols-2 gap-2">
              <button onClick={() => setModalOpen(false)} className="h-8 border border-borda-forte text-xs text-texto-2 hover:bg-superficie-2">Cancelar</button>
              <button onClick={salvar} disabled={!form.nome || (!editando && !form.clienteId) || salvando}
                className="h-8 bg-accent-600 hover:bg-accent-700 disabled:opacity-40 text-accent-fg text-xs font-medium">
                {salvando ? 'Salvando...' : 'Salvar'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
