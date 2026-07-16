import { useState, useEffect } from 'react'
import { configuracaoService, type MaquinaConfDto, type CampoMaquinaDto } from '../services/configuracaoService'
import { maquinaService, type MotivoParadaDto } from '../services/maquinaService'

type AbaModal = 'manual' | 'semi' | 'auto'

interface Props {
  open: boolean
  maquina: MaquinaConfDto | null
  onFechar: () => void
  onSalvo: () => void
}

const inputCls = 'w-full h-8 px-2 text-xs border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-zinc-900 dark:text-zinc-100 focus:outline-none focus:ring-1 focus:ring-blue-500'

export default function ConfiguracaoMaquinaModal({ open, maquina, onFechar, onSalvo }: Props) {
  const [abaModal, setAbaModal] = useState<AbaModal>('manual')
  const [form, setForm] = useState({
    nome: maquina?.nome ?? '',
    descricao: maquina?.descricao ?? ''
  })
  const [salvando, setSalvando] = useState(false)

  // Campos
  const [campos, setCampos] = useState<CampoMaquinaDto[]>([])
  const [loadingCampos, setLoadingCampos] = useState(false)
  const [modalCampoOpen, setModalCampoOpen] = useState(false)
  const [editandoCampo, setEditandoCampo] = useState<CampoMaquinaDto | null>(null)
  const [formCampo, setFormCampo] = useState({ nome: '', unidade: '', ordem: 1 })
  const [salvandoCampo, setSalvandoCampo] = useState(false)

  // Motivos
  const [motivos, setMotivos] = useState<MotivoParadaDto[]>([])
  const [loadingMotivos, setLoadingMotivos] = useState(false)
  const [modalMotivoOpen, setModalMotivoOpen] = useState(false)
  const [formMotivo, setFormMotivo] = useState({ nome: '', tipo: 'Interna' })
  const [salvandoMotivo, setSalvandoMotivo] = useState(false)

  useEffect(() => {
    if (!open || !maquina) return
    carregarCampos(maquina.id)
    carregarMotivos(maquina.id)
  }, [open, maquina])

  async function carregarCampos(id: string) {
    setLoadingCampos(true)
    try { setCampos(await configuracaoService.getCamposMaquina(id)) }
    finally { setLoadingCampos(false) }
  }

  async function carregarMotivos(id: string) {
    setLoadingMotivos(true)
    try { setMotivos(await maquinaService.getMotivosParada(id)) }
    finally { setLoadingMotivos(false) }
  }

  async function salvarDados() {
    setSalvando(true)
    try {
      const data = { nome: form.nome, descricao: form.descricao || null }
      if (maquina) {
        await configuracaoService.editarMaquina(maquina.id, { ...data, ativo: maquina.ativo })
      } else {
        await configuracaoService.criarMaquina(data)
      }
      onSalvo()
      onFechar()
    } finally { setSalvando(false) }
  }

  // ── Campos ────────────────────────────────────────────────
  function abrirNovoCampo() {
    setEditandoCampo(null)
    setFormCampo({ nome: '', unidade: '', ordem: campos.length + 1 })
    setModalCampoOpen(true)
  }

  function abrirEditarCampo(c: CampoMaquinaDto) {
    setEditandoCampo(c)
    setFormCampo({ nome: c.nome, unidade: c.unidade ?? '', ordem: c.ordem })
    setModalCampoOpen(true)
  }

  async function salvarCampo() {
    if (!maquina) return
    setSalvandoCampo(true)
    try {
      const data = { nome: formCampo.nome, unidade: formCampo.unidade || null, ordem: formCampo.ordem }
      if (editandoCampo) {
        await configuracaoService.editarCampoMaquina(editandoCampo.id, { ...data, ativo: editandoCampo.ativo })
      } else {
        await configuracaoService.criarCampoMaquina(maquina.id, data)
      }
      setModalCampoOpen(false)
      await carregarCampos(maquina.id)
    } finally { setSalvandoCampo(false) }
  }

  async function deletarCampo(id: string) {
    if (!maquina || !confirm('Desativar este campo?')) return
    await configuracaoService.deletarCampoMaquina(id)
    await carregarCampos(maquina.id)
  }

  // ── Motivos ───────────────────────────────────────────────
  function abrirNovoMotivo() {
    setFormMotivo({ nome: '', tipo: 'Interna' })
    setModalMotivoOpen(true)
  }

  async function salvarMotivo() {
    if (!maquina) return
    setSalvandoMotivo(true)
    try {
      await maquinaService.criarMotivoParada(maquina.id, formMotivo.nome, formMotivo.tipo)
      setModalMotivoOpen(false)
      await carregarMotivos(maquina.id)
    } finally { setSalvandoMotivo(false) }
  }

  async function deletarMotivo(id: string) {
    if (!maquina || !confirm('Remover este motivo?')) return
    setMotivos(prev => prev.filter(m => m.id !== id))
  }

  const motivosFiltrados = motivos.filter(m => m.tipo !== 'Planejada')

  if (!open) return null

  return (
    <>
      {/* Modal principal */}
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
        <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 w-[600px] max-h-[90vh] flex flex-col">

          {/* Header */}
          <div className="px-5 py-4 border-b border-zinc-200 dark:border-zinc-800">
            <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">
              {maquina ? 'Editar máquina' : 'Nova máquina'}
            </p>
          </div>

          {/* Dados básicos */}
          <div className="px-5 py-4 border-b border-zinc-200 dark:border-zinc-800">
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="text-xs text-zinc-500 mb-1 block">Nome</label>
                <input value={form.nome} onChange={e => setForm(f => ({ ...f, nome: e.target.value }))} className={inputCls} />
              </div>
              <div>
                <label className="text-xs text-zinc-500 mb-1 block">Descrição</label>
                <input value={form.descricao} onChange={e => setForm(f => ({ ...f, descricao: e.target.value }))} className={inputCls} />
              </div>
            </div>
          </div>

          {/* Abas — só ao editar */}
          {maquina && (
            <>
              <div className="flex justify-center border-b border-zinc-200 dark:border-zinc-800">
                {(['manual', 'semi', 'auto'] as AbaModal[]).map(a => (
                  <button
                    key={a}
                    onClick={() => setAbaModal(a)}
                    className={`px-5 py-2.5 text-xs font-medium border-b-2 transition-colors ${
                      abaModal === a
                        ? 'border-blue-600 text-blue-600 dark:text-blue-400'
                        : 'border-transparent text-zinc-500 hover:text-zinc-900 dark:hover:text-zinc-100'
                    }`}
                  >
                    {a === 'manual' ? 'Manual' : a === 'semi' ? 'Semi Automático' : 'Automático'}
                  </button>
                ))}
              </div>

              <div className="flex-1 overflow-y-auto px-5 py-4 min-h-[200px]">

                {/* Aba Manual */}
                {abaModal === 'manual' && (
                  <div className="flex gap-6">

                    {/* Campos de coleta */}
                    <div className="flex-1">
                      <div className="flex items-center justify-between mb-3">
                        <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">Campos de coleta</p>
                        <button onClick={abrirNovoCampo} className="h-7 px-2 bg-blue-600 hover:bg-blue-700 text-white text-[10px] flex items-center gap-1">
                          <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
                          Adicionar
                        </button>
                      </div>

                      {/* Produção fixo */}
                      <div className="flex items-center justify-between py-2 border-b border-zinc-100 dark:border-zinc-800">
                        <p className="text-xs text-zinc-900 dark:text-zinc-100">Produção</p>
                        <span className="text-[10px] px-1.5 py-0.5 bg-blue-50 dark:bg-blue-950 text-blue-600 dark:text-blue-400">fixo</span>
                      </div>

                      {loadingCampos ? (
                        <p className="text-xs text-zinc-400 mt-2">Carregando...</p>
                      ) : campos.length === 0 ? (
                        <p className="text-xs text-zinc-400 mt-2">Nenhum campo extra</p>
                      ) : campos.map(c => (
                        <div key={c.id} className="flex items-center justify-between py-2 border-b border-zinc-100 dark:border-zinc-800">
                          <div>
                            <p className="text-xs text-zinc-900 dark:text-zinc-100">
                              {c.nome} {c.unidade && <span className="text-zinc-400">({c.unidade})</span>}
                            </p>
                            <p className="text-[10px] text-zinc-400">ordem {c.ordem}</p>
                          </div>
                          <div className="flex items-center gap-1.5">
                            <button onClick={() => abrirEditarCampo(c)} className="text-zinc-400 hover:text-zinc-900 dark:hover:text-zinc-100">
                              <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
                            </button>
                            <button onClick={() => deletarCampo(c.id)} className="text-zinc-400 hover:text-red-500">
                              <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/></svg>
                            </button>
                          </div>
                        </div>
                      ))}
                    </div>

                    {/* Motivos de parada */}
                    <div className="flex-1 border-l border-zinc-200 dark:border-zinc-800 pl-6">
                      <div className="flex items-center justify-between mb-3">
                        <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">Motivos de parada</p>
                        <button onClick={abrirNovoMotivo} className="h-7 px-2 bg-blue-600 hover:bg-blue-700 text-white text-[10px] flex items-center gap-1">
                          <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
                          Adicionar
                        </button>
                      </div>

                      {loadingMotivos ? (
                        <p className="text-xs text-zinc-400">Carregando...</p>
                      ) : motivosFiltrados.length === 0 ? (
                        <p className="text-xs text-zinc-400">Nenhum motivo cadastrado</p>
                      ) : motivosFiltrados.map(m => (
                        <div key={m.id} className="flex items-center justify-between py-2 border-b border-zinc-100 dark:border-zinc-800">
                          <div>
                            <p className="text-xs text-zinc-900 dark:text-zinc-100">{m.nome}</p>
                            <span className={`text-[10px] ${m.tipo === 'Interna' ? 'text-blue-600 dark:text-blue-400' : 'text-amber-600 dark:text-amber-400'}`}>
                              {m.tipo === 'Interna' ? 'interna' : 'externa'}
                            </span>
                          </div>
                          <button onClick={() => deletarMotivo(m.id)} className="text-zinc-400 hover:text-red-500">
                            <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/></svg>
                          </button>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {abaModal === 'semi' && (
                  <div className="flex items-center justify-center h-32 text-xs text-zinc-400">Em desenvolvimento</div>
                )}

                {abaModal === 'auto' && (
                  <div className="flex items-center justify-center h-32 text-xs text-zinc-400">Em desenvolvimento</div>
                )}
              </div>
            </>
          )}

          {/* Footer */}
          <div className="px-5 py-3 border-t border-zinc-200 dark:border-zinc-800 flex justify-end gap-2">
            <button onClick={onFechar} className="h-8 px-4 border border-zinc-200 dark:border-zinc-700 text-xs text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800">
              Fechar
            </button>
            <button onClick={salvarDados} disabled={!form.nome || salvando} className="h-8 px-4 bg-blue-600 hover:bg-blue-700 disabled:opacity-40 text-white text-xs font-medium">
              {salvando ? 'Salvando...' : 'Salvar'}
            </button>
          </div>
        </div>
      </div>

      {/* Modal campo */}
      {modalCampoOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40">
          <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 w-72 p-5">
            <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100 mb-4">
              {editandoCampo ? 'Editar campo' : 'Novo campo'}
            </p>
            <div className="flex flex-col gap-3 mb-4">
              <div>
                <label className="text-xs text-zinc-500 mb-1 block">Nome</label>
                <input value={formCampo.nome} onChange={e => setFormCampo(f => ({ ...f, nome: e.target.value }))}
                  placeholder="Ex: Temperatura, Refugo..." className={inputCls} />
              </div>
              <div>
                <label className="text-xs text-zinc-500 mb-1 block">Unidade (opcional)</label>
                <input value={formCampo.unidade} onChange={e => setFormCampo(f => ({ ...f, unidade: e.target.value }))}
                  placeholder="Ex: °C, bar, kg..." className={inputCls} />
              </div>
              <div>
                <label className="text-xs text-zinc-500 mb-1 block">Ordem</label>
                <input type="number" min="1" value={formCampo.ordem}
                  onChange={e => setFormCampo(f => ({ ...f, ordem: parseInt(e.target.value) || 1 }))} className={inputCls} />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-2">
              <button onClick={() => setModalCampoOpen(false)} className="h-8 border border-zinc-200 dark:border-zinc-700 text-xs text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800">Cancelar</button>
              <button onClick={salvarCampo} disabled={!formCampo.nome || salvandoCampo}
                className="h-8 bg-blue-600 hover:bg-blue-700 disabled:opacity-40 text-white text-xs font-medium">
                {salvandoCampo ? 'Salvando...' : 'Salvar'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal motivo */}
      {modalMotivoOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40">
          <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 w-72 p-5">
            <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100 mb-4">Novo motivo de parada</p>
            <div className="flex flex-col gap-3 mb-4">
              <div>
                <label className="text-xs text-zinc-500 mb-1 block">Descrição</label>
                <input value={formMotivo.nome} onChange={e => setFormMotivo(f => ({ ...f, nome: e.target.value }))}
                  placeholder="Ex: Falta de matéria-prima..." className={inputCls} />
              </div>
              <div>
                <label className="text-xs text-zinc-500 mb-1 block">Tipo</label>
                <div className="grid grid-cols-2 gap-2">
                  <button onClick={() => setFormMotivo(f => ({ ...f, tipo: 'Interna' }))}
                    className={`h-8 text-xs font-medium border transition-colors ${formMotivo.tipo === 'Interna' ? 'bg-blue-600 text-white border-blue-600' : 'border-zinc-200 dark:border-zinc-700 text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800'}`}>
                    Interna
                  </button>
                  <button onClick={() => setFormMotivo(f => ({ ...f, tipo: 'Externa' }))}
                    className={`h-8 text-xs font-medium border transition-colors ${formMotivo.tipo === 'Externa' ? 'bg-amber-500 text-white border-amber-500' : 'border-zinc-200 dark:border-zinc-700 text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800'}`}>
                    Externa
                  </button>
                </div>
              </div>
            </div>
            <div className="grid grid-cols-2 gap-2">
              <button onClick={() => setModalMotivoOpen(false)} className="h-8 border border-zinc-200 dark:border-zinc-700 text-xs text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800">Cancelar</button>
              <button onClick={salvarMotivo} disabled={!formMotivo.nome || salvandoMotivo}
                className="h-8 bg-blue-600 hover:bg-blue-700 disabled:opacity-40 text-white text-xs font-medium">
                {salvandoMotivo ? 'Salvando...' : 'Salvar'}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}