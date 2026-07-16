import { useEffect, useState } from 'react'
import { configuracaoService, type MaquinaConfDto } from '../../services/configuracaoService'
import ConfiguracaoMaquinaModal from '../../modals/ConfiguracaoMaquinaModal'

export default function AbaMaquinas() {
  const [maquinas, setMaquinas] = useState<MaquinaConfDto[]>([])
  const [loading, setLoading] = useState(true)
  const [modalOpen, setModalOpen] = useState(false)
  const [editando, setEditando] = useState<MaquinaConfDto | null>(null)

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

  async function deletar(id: string) {
    if (!confirm('Desativar esta máquina?')) return
    await configuracaoService.deletarMaquina(id)
    await carregar()
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">Máquinas</p>
        <button onClick={abrirNovo} className="h-8 px-3 bg-blue-600 hover:bg-blue-700 text-white text-xs font-medium flex items-center gap-1.5">
          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
          Nova máquina
        </button>
      </div>

      {loading ? (
        <p className="text-xs text-zinc-400">Carregando...</p>
      ) : (
        <table className="w-full text-xs">
          <thead>
            <tr className="border-b border-zinc-200 dark:border-zinc-800">
              <th className="text-left py-2 pr-4 text-zinc-400 font-medium">Nome</th>
              <th className="text-left py-2 pr-4 text-zinc-400 font-medium">Descrição</th>
              <th className="text-left py-2 pr-4 text-zinc-400 font-medium">Status</th>
              <th className="py-2" />
            </tr>
          </thead>
          <tbody>
            {maquinas.map(m => (
              <tr key={m.id} className="border-b border-zinc-100 dark:border-zinc-800">
                <td className="py-2.5 pr-4 text-zinc-900 dark:text-zinc-100">{m.nome}</td>
                <td className="py-2.5 pr-4 text-zinc-500">{m.descricao ?? '—'}</td>
                <td className="py-2.5 pr-4">
                  <span className={`px-1.5 py-0.5 text-[10px] ${m.ativo ? 'bg-green-50 dark:bg-green-950 text-green-700 dark:text-green-400' : 'bg-zinc-100 dark:bg-zinc-800 text-zinc-400'}`}>
                    {m.ativo ? 'ativa' : 'inativa'}
                  </span>
                </td>
                <td className="py-2.5 flex items-center justify-end gap-2">
                  <button onClick={() => abrirEditar(m)} className="text-zinc-400 hover:text-zinc-900 dark:hover:text-zinc-100">
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
                  </button>
                  <button onClick={() => deletar(m.id)} className="text-zinc-400 hover:text-red-500">
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
    </div>
  )
}