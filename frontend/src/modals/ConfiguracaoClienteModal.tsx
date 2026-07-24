import { useState, useEffect } from 'react'
import {
  DndContext, closestCenter, PointerSensor, useSensor, useSensors,
  type DragEndEvent
} from '@dnd-kit/core'
import {
  SortableContext, verticalListSortingStrategy, useSortable, arrayMove
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { configuracaoService, type ClienteConfDto, type LinhaConfDto, type MaquinaConfDto } from '../services/configuracaoService'
import { linhaMaquinaService, type MaquinaLinhaConfDto } from '../services/linhaMaquinaService'

interface Props {
  open: boolean
  cliente: ClienteConfDto | null
  somenteLinhas?: boolean
  onFechar: () => void
  onSalvo: () => void
}

const ESTADOS_BR = ['AC','AL','AM','AP','BA','CE','DF','ES','GO','MA','MG','MS','MT','PA','PB','PE','PI','PR','RJ','RN','RO','RR','RS','SC','SE','SP','TO']

const inputCls = 'w-full h-8 px-2 text-xs border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-zinc-900 dark:text-zinc-100 focus:outline-none focus:ring-1 focus:ring-blue-500'

function tempId() {
  return `temp-${Date.now()}-${Math.random().toString(36).slice(2)}`
}

interface LinhaStaged extends LinhaConfDto {
  isNew: boolean
  isDeleted: boolean
}

interface MaquinaLinhaStaged extends MaquinaLinhaConfDto {
  isNew: boolean
  isDeleted: boolean
}

function SortableMaquinaItem({ item, onRemover }: { item: MaquinaLinhaStaged; onRemover: () => void }) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: item.id })
  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.5 : 1,
  }

  return (
    <div
      ref={setNodeRef}
      style={style}
      className="flex items-center gap-2 py-2 px-2 border-b border-zinc-100 dark:border-zinc-800 bg-white dark:bg-zinc-900"
    >
      <button {...attributes} {...listeners} className="cursor-grab text-zinc-400 hover:text-zinc-600 dark:hover:text-zinc-300 touch-none">
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <line x1="8" y1="6" x2="16" y2="6"/><line x1="8" y1="12" x2="16" y2="12"/><line x1="8" y1="18" x2="16" y2="18"/>
        </svg>
      </button>
      <div className="flex-1">
        <p className="text-xs text-zinc-900 dark:text-zinc-100">
          {item.maquinaNome} {item.isNew && <span className="text-[9px] text-blue-500">(novo)</span>}
        </p>
        <p className="text-[10px] text-zinc-400">
          {item.velocidadeNominal} un/h {item.sobreVelocidade > 0 && `+ ${item.sobreVelocidade}%`}
        </p>
      </div>
      {item.critica && (
        <span className="text-[10px] px-1.5 py-0.5 bg-blue-50 dark:bg-blue-950 text-blue-600 dark:text-blue-400">crítica</span>
      )}
      <button onClick={onRemover} className="text-zinc-400 hover:text-red-500">
        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/></svg>
      </button>
    </div>
  )
}

export default function ConfiguracaoClienteModal({ open, cliente, somenteLinhas, onFechar, onSalvo }: Props) {
  const [form, setForm] = useState({ nome: cliente?.nome ?? '', estado: cliente?.estado ?? '' })
  const [salvando, setSalvando] = useState(false)

  const [linhas, setLinhas] = useState<LinhaStaged[]>([])
  const [loadingLinhas, setLoadingLinhas] = useState(false)
  const [linhaExpandida, setLinhaExpandida] = useState<string | null>(null)
  const [maquinasPorLinha, setMaquinasPorLinha] = useState<Record<string, MaquinaLinhaStaged[]>>({})
  const [loadingMaquinas, setLoadingMaquinas] = useState<string | null>(null)

  const [modalNovaLinhaOpen, setModalNovaLinhaOpen] = useState(false)
  const [novaLinhaNome, setNovaLinhaNome] = useState('')

  const [modalAdicionarMaquinaOpen, setModalAdicionarMaquinaOpen] = useState(false)
  const [linhaAlvo, setLinhaAlvo] = useState<string | null>(null)
  const [todasMaquinas, setTodasMaquinas] = useState<MaquinaConfDto[]>([])
  const [formMaquina, setFormMaquina] = useState({ maquinaId: '', critica: false, velocidadeNominal: '', sobreVelocidade: '0' })

  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 5 } }))

  useEffect(() => {
    if (!open || !cliente) return
    setForm({ nome: cliente.nome, estado: cliente.estado ?? '' })
    setMaquinasPorLinha({})
    setLinhaExpandida(null)
    carregarLinhas()
  }, [open, cliente])

  async function carregarLinhas() {
    if (!cliente) return
    setLoadingLinhas(true)
    try {
      const todas = await configuracaoService.getLinhas()
      const doCliente = todas.filter(l => l.clienteId === cliente.id)
      setLinhas(doCliente.map(l => ({ ...l, isNew: false, isDeleted: false })))
    } finally {
      setLoadingLinhas(false)
    }
  }

  async function toggleExpandirLinha(linha: LinhaStaged) {
    if (linhaExpandida === linha.id) {
      setLinhaExpandida(null)
      return
    }
    setLinhaExpandida(linha.id)
    if (!maquinasPorLinha[linha.id]) {
      if (linha.isNew) {
        setMaquinasPorLinha(prev => ({ ...prev, [linha.id]: [] }))
        return
      }
      setLoadingMaquinas(linha.id)
      try {
        const maquinas = await linhaMaquinaService.getMaquinas(linha.id)
        setMaquinasPorLinha(prev => ({
          ...prev,
          [linha.id]: maquinas.map(m => ({ ...m, isNew: false, isDeleted: false }))
        }))
      } finally {
        setLoadingMaquinas(null)
      }
    }
  }

  function adicionarLinhaLocal() {
    if (!novaLinhaNome.trim() || !cliente) return
    const id = tempId()
    setLinhas(prev => [...prev, { id, nome: novaLinhaNome.trim(), clienteId: cliente.id, clienteNome: cliente.nome, ativo: true, isNew: true, isDeleted: false }])
    setModalNovaLinhaOpen(false)
    setNovaLinhaNome('')
  }

  function removerLinhaLocal(linha: LinhaStaged) {
    if (!confirm('Remover esta linha?')) return
    if (linha.isNew) {
      setLinhas(prev => prev.filter(l => l.id !== linha.id))
      setMaquinasPorLinha(prev => {
        const copia = { ...prev }
        delete copia[linha.id]
        return copia
      })
    } else {
      setLinhas(prev => prev.map(l => l.id === linha.id ? { ...l, isDeleted: true } : l))
    }
    if (linhaExpandida === linha.id) setLinhaExpandida(null)
  }

  async function abrirAdicionarMaquina(linhaId: string) {
    setLinhaAlvo(linhaId)
    setFormMaquina({ maquinaId: '', critica: false, velocidadeNominal: '', sobreVelocidade: '0' })
    if (todasMaquinas.length === 0) {
      const maquinas = await configuracaoService.getMaquinas()
      setTodasMaquinas(maquinas.filter(m => m.ativo))
    }
    setModalAdicionarMaquinaOpen(true)
  }

  function adicionarMaquinaLocal() {
    if (!linhaAlvo || !formMaquina.maquinaId) return
    const maquinaInfo = todasMaquinas.find(m => m.id === formMaquina.maquinaId)
    const itensAtuais = maquinasPorLinha[linhaAlvo] ?? []
    const novoItem: MaquinaLinhaStaged = {
      id: tempId(),
      linhaId: linhaAlvo,
      maquinaId: formMaquina.maquinaId,
      maquinaNome: maquinaInfo?.nome ?? '',
      ordem: itensAtuais.length + 1,
      critica: formMaquina.critica,
      velocidadeNominal: Number(formMaquina.velocidadeNominal) || 0,
      sobreVelocidade: Number(formMaquina.sobreVelocidade) || 0,
      ativo: true,
      isNew: true,
      isDeleted: false,
    }
    setMaquinasPorLinha(prev => ({ ...prev, [linhaAlvo]: [...itensAtuais, novoItem] }))
    setModalAdicionarMaquinaOpen(false)
  }

  function removerMaquinaLocal(linhaId: string, maquinaLinhaId: string) {
    if (!confirm('Remover esta máquina da linha?')) return
    setMaquinasPorLinha(prev => {
      const item = (prev[linhaId] ?? []).find(m => m.id === maquinaLinhaId)
      if (!item) return prev
      if (item.isNew) {
        return { ...prev, [linhaId]: prev[linhaId].filter(m => m.id !== maquinaLinhaId) }
      }
      return { ...prev, [linhaId]: prev[linhaId].map(m => m.id === maquinaLinhaId ? { ...m, isDeleted: true } : m) }
    })
  }

  function handleDragEnd(linhaId: string, event: DragEndEvent) {
    const { active, over } = event
    if (!over || active.id === over.id) return

    setMaquinasPorLinha(prev => {
      const itens = prev[linhaId] ?? []
      const oldIndex = itens.findIndex(i => i.id === active.id)
      const newIndex = itens.findIndex(i => i.id === over.id)
      const reordenado = arrayMove(itens, oldIndex, newIndex).map((item, idx) => ({ ...item, ordem: idx + 1 }))
      return { ...prev, [linhaId]: reordenado }
    })
  }

  async function salvarTudo() {
    if (!cliente) return
    setSalvando(true)
    try {
      // 1. Dados do cliente
      if (!somenteLinhas) {
        await configuracaoService.editarCliente(cliente.id, { nome: form.nome, estado: form.estado || null, ativo: cliente.ativo })
      }

      // 2. Linhas: remove deletadas existentes, cria novas
      const mapaIdLinha: Record<string, string> = {}
      for (const linha of linhas) {
        if (linha.isDeleted && !linha.isNew) {
          await configuracaoService.deletarLinha(linha.id)
        } else if (linha.isNew && !linha.isDeleted) {
          const criada = await configuracaoService.criarLinha({ nome: linha.nome, clienteId: cliente.id })
          mapaIdLinha[linha.id] = criada.id
        }
      }

      // 3. Máquinas por linha
      for (const linha of linhas) {
        if (linha.isDeleted) continue
        const linhaRealId = linha.isNew ? mapaIdLinha[linha.id] : linha.id
        if (!linhaRealId) continue

        const itens = maquinasPorLinha[linha.id] ?? []
        for (const item of itens) {
          if (item.isDeleted && !item.isNew) {
            await linhaMaquinaService.remover(linhaRealId, item.id)
          } else if (item.isNew && !item.isDeleted) {
            await linhaMaquinaService.adicionar(linhaRealId, item.maquinaId, item.critica, item.velocidadeNominal, item.sobreVelocidade)
          } else if (!item.isNew && !item.isDeleted) {
            await linhaMaquinaService.atualizar(linhaRealId, item.id, item.critica, item.velocidadeNominal, item.sobreVelocidade)
          }
        }
      }

      onSalvo()
      onFechar()
    } finally {
      setSalvando(false)
    }
  }

  if (!open || !cliente) return null

  const linhasVisiveis = linhas.filter(l => !l.isDeleted)

  return (
    <>
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
        <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 w-[560px] max-h-[90vh] flex flex-col">

          <div className="px-5 py-4 border-b border-zinc-200 dark:border-zinc-800">
            <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">
              {somenteLinhas ? cliente.nome : 'Editar cliente'}
            </p>
          </div>

          {!somenteLinhas && (
            <div className="px-5 py-4 border-b border-zinc-200 dark:border-zinc-800">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="text-xs text-zinc-500 mb-1 block">Nome</label>
                  <input value={form.nome} onChange={e => setForm(f => ({ ...f, nome: e.target.value }))} className={inputCls} />
                </div>
                <div>
                  <label className="text-xs text-zinc-500 mb-1 block">Estado</label>
                  <select value={form.estado} onChange={e => setForm(f => ({ ...f, estado: e.target.value }))} className={inputCls}>
                    <option value="">Selecione</option>
                    {ESTADOS_BR.map(e => <option key={e} value={e}>{e}</option>)}
                  </select>
                </div>
              </div>
            </div>
          )}

          <div className="flex-1 overflow-y-auto px-5 py-4">
            <div className="flex items-center justify-between mb-3">
              <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">Linhas de produção</p>
              <button onClick={() => setModalNovaLinhaOpen(true)} className="h-7 px-2 bg-blue-600 hover:bg-blue-700 text-white text-[10px] flex items-center gap-1">
                <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
                Nova linha
              </button>
            </div>

            {loadingLinhas ? (
              <p className="text-xs text-zinc-400">Carregando...</p>
            ) : linhasVisiveis.length === 0 ? (
              <p className="text-xs text-zinc-400">Nenhuma linha cadastrada</p>
            ) : (
              <div className="flex flex-col gap-2">
                {linhasVisiveis.map(linha => (
                  <div key={linha.id} className="border border-zinc-200 dark:border-zinc-800">
                    <div className="flex items-center justify-between px-3 py-2 bg-zinc-50 dark:bg-zinc-800/50">
                      <button
                        onClick={() => toggleExpandirLinha(linha)}
                        className="flex items-center gap-2 text-xs text-zinc-900 dark:text-zinc-100 font-medium"
                      >
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"
                          style={{ transform: linhaExpandida === linha.id ? 'rotate(90deg)' : 'none', transition: 'transform 0.15s' }}>
                          <polyline points="9 18 15 12 9 6"/>
                        </svg>
                        {linha.nome}
                        {linha.isNew && <span className="text-[9px] text-blue-500">(novo)</span>}
                      </button>
                      <div className="flex items-center gap-2">
                        <span className={`text-[10px] px-1.5 py-0.5 ${linha.ativo ? 'bg-green-50 dark:bg-green-950 text-green-700 dark:text-green-400' : 'bg-zinc-100 dark:bg-zinc-800 text-zinc-400'}`}>
                          {linha.ativo ? 'ativa' : 'inativa'}
                        </span>
                        <button onClick={() => removerLinhaLocal(linha)} className="text-zinc-400 hover:text-red-500">
                          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/></svg>
                        </button>
                      </div>
                    </div>

                    {linhaExpandida === linha.id && (
                      <div>
                        {loadingMaquinas === linha.id ? (
                          <p className="text-xs text-zinc-400 px-3 py-2">Carregando máquinas...</p>
                        ) : (
                          <>
                            {(maquinasPorLinha[linha.id] ?? []).filter(m => !m.isDeleted).length === 0 ? (
                              <p className="text-xs text-zinc-400 px-3 py-2">Nenhuma máquina nesta linha</p>
                            ) : (
                              <DndContext
                                sensors={sensors}
                                collisionDetection={closestCenter}
                                onDragEnd={(e) => handleDragEnd(linha.id, e)}
                              >
                                <SortableContext
                                  items={(maquinasPorLinha[linha.id] ?? []).filter(m => !m.isDeleted).map(m => m.id)}
                                  strategy={verticalListSortingStrategy}
                                >
                                  {(maquinasPorLinha[linha.id] ?? []).filter(m => !m.isDeleted).map(item => (
                                    <SortableMaquinaItem
                                      key={item.id}
                                      item={item}
                                      onRemover={() => removerMaquinaLocal(linha.id, item.id)}
                                    />
                                  ))}
                                </SortableContext>
                              </DndContext>
                            )}
                            <button
                              onClick={() => abrirAdicionarMaquina(linha.id)}
                              className="w-full flex items-center justify-center gap-1.5 py-2 text-[10px] text-zinc-500 border-t border-dashed border-zinc-200 dark:border-zinc-700 hover:bg-zinc-50 dark:hover:bg-zinc-800"
                            >
                              <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
                              Adicionar máquina
                            </button>
                          </>
                        )}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="px-5 py-3 border-t border-zinc-200 dark:border-zinc-800 flex justify-end gap-2">
            <button onClick={onFechar} disabled={salvando} className="h-8 px-4 border border-zinc-200 dark:border-zinc-700 text-xs text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800 disabled:opacity-50">
              Cancelar
            </button>
            <button onClick={salvarTudo} disabled={(!somenteLinhas && !form.nome) || salvando} className="h-8 px-4 bg-blue-600 hover:bg-blue-700 disabled:opacity-40 text-white text-xs font-medium">
              {salvando ? 'Salvando...' : 'Salvar'}
            </button>
          </div>
        </div>
      </div>

      {/* Modal nova linha */}
      {modalNovaLinhaOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40">
          <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 w-72 p-5">
            <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100 mb-4">Nova linha</p>
            <div className="mb-4">
              <label className="text-xs text-zinc-500 mb-1 block">Nome da linha</label>
              <input
                value={novaLinhaNome}
                onChange={e => setNovaLinhaNome(e.target.value)}
                placeholder="Ex: Linha 1, Linha 504..."
                autoFocus
                className={inputCls}
              />
            </div>
            <div className="grid grid-cols-2 gap-2">
              <button onClick={() => { setModalNovaLinhaOpen(false); setNovaLinhaNome('') }} className="h-8 border border-zinc-200 dark:border-zinc-700 text-xs text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800">
                Cancelar
              </button>
              <button onClick={adicionarLinhaLocal} disabled={!novaLinhaNome.trim()} className="h-8 bg-blue-600 hover:bg-blue-700 disabled:opacity-40 text-white text-xs font-medium">
                Adicionar
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal adicionar máquina */}
      {modalAdicionarMaquinaOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/40">
          <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 w-80 p-5">
            <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100 mb-4">Adicionar máquina</p>
            <div className="flex flex-col gap-3 mb-4">
              <div>
                <label className="text-xs text-zinc-500 mb-1 block">Máquina</label>
                <select
                  value={formMaquina.maquinaId}
                  onChange={e => setFormMaquina(f => ({ ...f, maquinaId: e.target.value }))}
                  className={inputCls}
                >
                  <option value="">Selecione...</option>
                  {todasMaquinas.map(m => <option key={m.id} value={m.id}>{m.nome}</option>)}
                </select>
              </div>
              <div>
                <label className="text-xs text-zinc-500 mb-1 block">Velocidade nominal (prod/h)</label>
                <input
                  type="number" min="0"
                  value={formMaquina.velocidadeNominal}
                  onChange={e => setFormMaquina(f => ({ ...f, velocidadeNominal: e.target.value }))}
                  placeholder="Ex: 1200"
                  className={inputCls}
                />
              </div>
              <div>
                <label className="text-xs text-zinc-500 mb-1 block">Sobre velocidade (%)</label>
                <input
                  type="number" min="0" max="100"
                  value={formMaquina.sobreVelocidade}
                  onChange={e => setFormMaquina(f => ({ ...f, sobreVelocidade: e.target.value }))}
                  className={inputCls}
                />
              </div>
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={formMaquina.critica}
                  onChange={e => setFormMaquina(f => ({ ...f, critica: e.target.checked }))}
                  className="accent-blue-600"
                />
                <span className="text-xs text-zinc-900 dark:text-zinc-100">Máquina crítica</span>
              </label>
            </div>
            <div className="grid grid-cols-2 gap-2">
              <button onClick={() => setModalAdicionarMaquinaOpen(false)} className="h-8 border border-zinc-200 dark:border-zinc-700 text-xs text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800">
                Cancelar
              </button>
              <button
                onClick={adicionarMaquinaLocal}
                disabled={!formMaquina.maquinaId || !formMaquina.velocidadeNominal}
                className="h-8 bg-blue-600 hover:bg-blue-700 disabled:opacity-40 text-white text-xs font-medium"
              >
                Adicionar
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}