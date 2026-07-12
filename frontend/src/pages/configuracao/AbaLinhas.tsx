import { useEffect, useState } from 'react'
import { configuracaoService, type LinhaConfDto, type ClienteConfDto } from '../../services/configuracaoService'

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
        <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">Linhas</p>
        <button onClick={abrirNovo} className="h-8 px-3 bg-blue-600 hover:bg-blue-700 text-white text-xs font-medium flex items-center gap-1.5">
          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
          Nova linha
        </button>
      </div>

      {loading ? (
        <p className="text-xs text-zinc-400">Carregando...</p>
      ) : (
        <table className="w-full text-xs">
          <thead>
            <tr className="border-b border-zinc-200 dark:border-zinc-800">
              <th className="text-left py-2 pr-4 text-zinc-400 font-medium">Nome</th>
              <th className="text-left py-2 pr-4 text-zinc-400 font-medium">Cliente</th>
              <th className="text-left py-2 pr-4 text-zinc-400 font-medium">Status</th>
              <th className="py-2" />
            </tr>
          </thead>
          <tbody>
            {linhas.map(l => (
              <tr key={l.id} className="border-b border-zinc-100 dark:border-zinc-800">
                <td className="py-2.5 pr-4 text-zinc-900 dark:text-zinc-100">{l.nome}</td>
                <td className="py-2.5 pr-4 text-zinc-500">{l.clienteNome}</td>
                <td className="py-2.5 pr-4">
                  <span className={`px-1.5 py-0.5 text-[10px] ${l.ativo ? 'bg-green-50 dark:bg-green-950 text-green-700 dark:text-green-400' : 'bg-zinc-100 dark:bg-zinc-800 text-zinc-400'}`}>
                    {l.ativo ? 'ativa' : 'inativa'}
                  </span>
                </td>
                <td className="py-2.5 flex items-center justify-end gap-2">
                  <button onClick={() => abrirEditar(l)} className="text-zinc-400 hover:text-zinc-900 dark:hover:text-zinc-100">
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
                  </button>
                  <button onClick={() => deletar(l.id)} className="text-zinc-400 hover:text-red-500">
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
          <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 w-80 p-5">
            <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100 mb-4">
              {editando ? 'Editar linha' : 'Nova linha'}
            </p>
            <div className="flex flex-col gap-3 mb-4">
              <div>
                <label className="text-xs text-zinc-500 mb-1 block">Nome</label>
                <input value={form.nome} onChange={e => setForm(f => ({ ...f, nome: e.target.value }))}
                  className="w-full h-8 px-2 text-xs border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-zinc-900 dark:text-zinc-100 focus:outline-none focus:ring-1 focus:ring-blue-500" />
              </div>
              {!editando && (
                <div>
                  <label className="text-xs text-zinc-500 mb-1 block">Cliente</label>
                  <select value={form.clienteId} onChange={e => setForm(f => ({ ...f, clienteId: e.target.value }))}
                    className="w-full h-8 px-2 text-xs border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-zinc-900 dark:text-zinc-100 focus:outline-none focus:ring-1 focus:ring-blue-500">
                    {clientes.filter(c => c.ativo).map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
                  </select>
                </div>
              )}
            </div>
            <div className="grid grid-cols-2 gap-2">
              <button onClick={() => setModalOpen(false)} className="h-8 border border-zinc-200 dark:border-zinc-700 text-xs text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800">Cancelar</button>
              <button onClick={salvar} disabled={!form.nome || (!editando && !form.clienteId) || salvando}
                className="h-8 bg-blue-600 hover:bg-blue-700 disabled:opacity-40 text-white text-xs font-medium">
                {salvando ? 'Salvando...' : 'Salvar'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
