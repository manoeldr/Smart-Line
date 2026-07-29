// Modal de criação/edição de máquina.
// Dados básicos (nome, descrição) ficam sempre visíveis.
// Ao editar uma máquina existente, aparecem abas Manual/Semi Automático/Automático
// com a configuração específica de cada modo de coleta.
// Segue o padrão "staged changes": campos e motivos só são persistidos no banco ao clicar em Salvar.
import { useState, useEffect } from 'react'
import { configuracaoService, type MaquinaConfDto, type CampoMaquinaDto } from '../services/configuracaoService'
import { maquinaService, type MotivoParadaDto } from '../services/maquinaService'
import { btnPrimary, btnPrimaryXs, btnSecondarySm, btnIconDanger } from '../styles/buttons'
import { inputBase, label } from '../styles/inputs'
import { badgeFixo, badgeInterna, badgeExterna, badgeNovo } from '../styles/badges'
import { modalOverlay, modalOverlayNested, modalContainerSm, modalPanel, modalHeader, modalTitle, modalFooter } from '../styles/modals'
import { tabButton } from '../styles/tables'

type AbaModal = 'manual' | 'semi' | 'auto'

interface Props {
  open: boolean
  maquina: MaquinaConfDto | null
  onFechar: () => void
  onSalvo: () => void
}

// Um campo/motivo "staged" carrega flags isNew/isDeleted para controlar
// o que precisa ser criado ou removido no banco só na hora de salvar.
function tempId() {
  return `temp-${Date.now()}-${Math.random().toString(36).slice(2)}`
}

interface CampoStaged extends CampoMaquinaDto {
  isNew: boolean
  isDeleted: boolean
}

interface MotivoStaged extends MotivoParadaDto {
  isNew: boolean
  isDeleted: boolean
}

export default function ConfiguracaoMaquinaModal({ open, maquina, onFechar, onSalvo }: Props) {
  const [abaModal, setAbaModal] = useState<AbaModal>('manual')
  const [form, setForm] = useState({
    nome: maquina?.nome ?? '',
    descricao: maquina?.descricao ?? ''
  })
  const [salvando, setSalvando] = useState(false)

  // Campos de coleta (staged)
  const [campos, setCampos] = useState<CampoStaged[]>([])
  const [loadingCampos, setLoadingCampos] = useState(false)
  const [modalCampoOpen, setModalCampoOpen] = useState(false)
  const [formCampo, setFormCampo] = useState({ nome: '', unidade: '', ordem: 1 })

  // Motivos de parada (staged)
  const [motivos, setMotivos] = useState<MotivoStaged[]>([])
  const [loadingMotivos, setLoadingMotivos] = useState(false)
  const [modalMotivoOpen, setModalMotivoOpen] = useState(false)
  const [formMotivo, setFormMotivo] = useState({ nome: '', tipo: 'Interna' })

  // Ao abrir o modal (ou trocar de máquina), reseta o formulário e carrega dados existentes
  useEffect(() => {
    if (!open) return
    setForm({ nome: maquina?.nome ?? '', descricao: maquina?.descricao ?? '' })
    if (maquina) {
      carregarCampos(maquina.id)
      carregarMotivos(maquina.id)
    } else {
      setCampos([])
      setMotivos([])
    }
  }, [open, maquina])

  async function carregarCampos(id: string) {
    setLoadingCampos(true)
    try {
      const data = await configuracaoService.getCamposMaquina(id)
      setCampos(data.map(c => ({ ...c, isNew: false, isDeleted: false })))
    } finally { setLoadingCampos(false) }
  }

  async function carregarMotivos(id: string) {
    setLoadingMotivos(true)
    try {
      const data = await maquinaService.getMotivosParada(id)
      setMotivos(data.map(m => ({ ...m, isNew: false, isDeleted: false })))
    } finally { setLoadingMotivos(false) }
  }

  // ── Campos — adiciona/remove só em memória; persiste no banco ao Salvar ──
  function abrirNovoCampo() {
    setFormCampo({ nome: '', unidade: '', ordem: campos.filter(c => !c.isDeleted).length + 1 })
    setModalCampoOpen(true)
  }

  function adicionarCampoLocal() {
    if (!formCampo.nome.trim()) return
    setCampos(prev => [...prev, {
      id: tempId(),
      maquinaId: maquina?.id ?? '',
      nome: formCampo.nome.trim(),
      unidade: formCampo.unidade || null,
      ordem: formCampo.ordem,
      ativo: true,
      isNew: true,
      isDeleted: false,
    }])
    setModalCampoOpen(false)
  }

  function removerCampoLocal(id: string) {
    if (!confirm('Remover este campo?')) return
    setCampos(prev => {
      const item = prev.find(c => c.id === id)
      if (!item) return prev
      // Campo novo (ainda não salvo): remove direto da lista.
      // Campo existente: marca como deletado, só remove do banco ao Salvar.
      if (item.isNew) return prev.filter(c => c.id !== id)
      return prev.map(c => c.id === id ? { ...c, isDeleted: true } : c)
    })
  }

  // ── Motivos — mesmo padrão staged dos campos ──
  function abrirNovoMotivo() {
    setFormMotivo({ nome: '', tipo: 'Interna' })
    setModalMotivoOpen(true)
  }

  function adicionarMotivoLocal() {
    if (!formMotivo.nome.trim()) return
    setMotivos(prev => [...prev, {
      id: tempId(),
      nome: formMotivo.nome.trim(),
      tipo: formMotivo.tipo,
      isNew: true,
      isDeleted: false,
    }])
    setModalMotivoOpen(false)
  }

  function removerMotivoLocal(id: string) {
    if (!confirm('Remover este motivo?')) return
    setMotivos(prev => {
      const item = prev.find(m => m.id === id)
      if (!item) return prev
      if (item.isNew) return prev.filter(m => m.id !== id)
      return prev.map(m => m.id === id ? { ...m, isDeleted: true } : m)
    })
  }

  // Persiste tudo de uma vez: dados da máquina, campos e motivos pendentes
  async function salvarTudo() {
    setSalvando(true)
    try {
      const data = { nome: form.nome, descricao: form.descricao || null }
      let maquinaId = maquina?.id

      if (maquina) {
        await configuracaoService.editarMaquina(maquina.id, { ...data, ativo: maquina.ativo })
      } else {
        const criada = await configuracaoService.criarMaquina(data)
        maquinaId = criada.id
      }

      if (maquinaId) {
        for (const c of campos) {
          if (c.isDeleted && !c.isNew) {
            await configuracaoService.deletarCampoMaquina(c.id)
          } else if (c.isNew && !c.isDeleted) {
            await configuracaoService.criarCampoMaquina(maquinaId, { nome: c.nome, unidade: c.unidade, ordem: c.ordem })
          }
        }

        for (const m of motivos) {
          if (m.isDeleted && !m.isNew) {
            await maquinaService.deletarMotivoParada(m.id)
          } else if (m.isNew && !m.isDeleted) {
            await maquinaService.criarMotivoParada(maquinaId, m.nome, m.tipo)
          }
        }
      }

      onSalvo()
      onFechar()
    } finally { setSalvando(false) }
  }

  const camposVisiveis = campos.filter(c => !c.isDeleted)
  // Motivos planejados não aparecem aqui — são compartilhados por linha e geridos na tela de Medição
  const motivosVisiveis = motivos.filter(m => !m.isDeleted && m.tipo !== 'Planejada')

  if (!open) return null

  return (
    <>
      {/* Modal principal */}
      <div className={modalOverlay}>
        <div className={`${modalPanel} w-[600px] max-h-[90vh]`}>

          <div className={modalHeader}>
            <p className={modalTitle}>
              {maquina ? 'Editar máquina' : 'Nova máquina'}
            </p>
          </div>

          {/* Dados básicos */}
          <div className="px-5 py-4 border-b border-zinc-200 dark:border-zinc-800">
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className={label}>Nome</label>
                <input value={form.nome} onChange={e => setForm(f => ({ ...f, nome: e.target.value }))} className={inputBase} />
              </div>
              <div>
                <label className={label}>Descrição</label>
                <input value={form.descricao} onChange={e => setForm(f => ({ ...f, descricao: e.target.value }))} className={inputBase} />
              </div>
            </div>
          </div>

          {/* Abas — só aparecem ao editar uma máquina existente */}
          {maquina && (
            <>
              <div className="flex justify-center border-b border-zinc-200 dark:border-zinc-800">
                {(['manual', 'semi', 'auto'] as AbaModal[]).map(a => (
                  <button key={a} onClick={() => setAbaModal(a)} className={tabButton(abaModal === a)}>
                    {a === 'manual' ? 'Manual' : a === 'semi' ? 'Semi Automático' : 'Automático'}
                  </button>
                ))}
              </div>

              <div className="flex-1 overflow-y-auto px-5 py-4 min-h-[200px]">

                {abaModal === 'manual' && (
                  <div className="flex gap-6">

                    {/* Campos de coleta */}
                    <div className="flex-1">
                      <div className="flex items-center justify-between mb-3">
                        <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">Campos de coleta</p>
                        <button onClick={abrirNovoCampo} className={`${btnPrimaryXs} flex items-center gap-1`}>
                          <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
                          Adicionar
                        </button>
                      </div>

                      {/* Produção é sempre fixo — não pode ser removido, coletado em toda medição */}
                      <div className="flex items-center justify-between py-2 border-b border-zinc-100 dark:border-zinc-800">
                        <p className="text-xs text-zinc-900 dark:text-zinc-100">Produção</p>
                        <span className={badgeFixo}>fixo</span>
                      </div>

                      {loadingCampos ? (
                        <p className="text-xs text-zinc-400 mt-2">Carregando...</p>
                      ) : camposVisiveis.length === 0 ? (
                        <p className="text-xs text-zinc-400 mt-2">Nenhum campo extra</p>
                      ) : camposVisiveis.map(c => (
                        <div key={c.id} className="flex items-center justify-between py-2 border-b border-zinc-100 dark:border-zinc-800">
                          <div>
                            <p className="text-xs text-zinc-900 dark:text-zinc-100">
                              {c.nome} {c.unidade && <span className="text-zinc-400">({c.unidade})</span>}
                              {c.isNew && <span className={`${badgeNovo} ml-1`}>(novo)</span>}
                            </p>
                            <p className="text-[10px] text-zinc-400">ordem {c.ordem}</p>
                          </div>
                          <button onClick={() => removerCampoLocal(c.id)} className={btnIconDanger}>
                            <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/></svg>
                          </button>
                        </div>
                      ))}
                    </div>

                    {/* Motivos de parada */}
                    <div className="flex-1 border-l border-zinc-200 dark:border-zinc-800 pl-6">
                      <div className="flex items-center justify-between mb-3">
                        <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">Motivos de parada</p>
                        <button onClick={abrirNovoMotivo} className={`${btnPrimaryXs} flex items-center gap-1`}>
                          <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
                          Adicionar
                        </button>
                      </div>

                      {loadingMotivos ? (
                        <p className="text-xs text-zinc-400">Carregando...</p>
                      ) : motivosVisiveis.length === 0 ? (
                        <p className="text-xs text-zinc-400">Nenhum motivo cadastrado</p>
                      ) : motivosVisiveis.map(m => (
                        <div key={m.id} className="flex items-center justify-between py-2 border-b border-zinc-100 dark:border-zinc-800">
                          <div>
                            <p className="text-xs text-zinc-900 dark:text-zinc-100">
                              {m.nome} {m.isNew && <span className={badgeNovo}>(novo)</span>}
                            </p>
                            <span className={m.tipo === 'Interna' ? badgeInterna : badgeExterna}>
                              {m.tipo === 'Interna' ? 'interna' : 'externa'}
                            </span>
                          </div>
                          <button onClick={() => removerMotivoLocal(m.id)} className={btnIconDanger}>
                            <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/></svg>
                          </button>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Ainda não implementados — reservados para os próximos modos de coleta */}
                {abaModal === 'semi' && (
                  <div className="flex items-center justify-center h-32 text-xs text-zinc-400">Em desenvolvimento</div>
                )}

                {abaModal === 'auto' && (
                  <div className="flex items-center justify-center h-32 text-xs text-zinc-400">Em desenvolvimento</div>
                )}
              </div>
            </>
          )}

          <div className={modalFooter}>
            <button onClick={onFechar} disabled={salvando} className={btnSecondarySm}>
              Cancelar
            </button>
            <button onClick={salvarTudo} disabled={!form.nome || salvando} className={btnPrimary}>
              {salvando ? 'Salvando...' : 'Salvar'}
            </button>
          </div>
        </div>
      </div>

      {/* Modal — novo campo de coleta */}
      {modalCampoOpen && (
        <div className={modalOverlayNested}>
          <div className={modalContainerSm}>
            <p className={`${modalTitle} mb-4`}>Novo campo</p>
            <div className="flex flex-col gap-3 mb-4">
              <div>
                <label className={label}>Nome</label>
                <input value={formCampo.nome} onChange={e => setFormCampo(f => ({ ...f, nome: e.target.value }))}
                  placeholder="Ex: Temperatura, Refugo..." className={inputBase} />
              </div>
              <div>
                <label className={label}>Unidade (opcional)</label>
                <input value={formCampo.unidade} onChange={e => setFormCampo(f => ({ ...f, unidade: e.target.value }))}
                  placeholder="Ex: °C, bar, kg..." className={inputBase} />
              </div>
              <div>
                <label className={label}>Ordem</label>
                <input type="number" min="1" value={formCampo.ordem}
                  onChange={e => setFormCampo(f => ({ ...f, ordem: parseInt(e.target.value) || 1 }))} className={inputBase} />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-2">
              <button onClick={() => setModalCampoOpen(false)} className={btnSecondarySm}>Cancelar</button>
              <button onClick={adicionarCampoLocal} disabled={!formCampo.nome} className={btnPrimary}>
                Adicionar
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal — novo motivo de parada */}
      {modalMotivoOpen && (
        <div className={modalOverlayNested}>
          <div className={modalContainerSm}>
            <p className={`${modalTitle} mb-4`}>Novo motivo de parada</p>
            <div className="flex flex-col gap-3 mb-4">
              <div>
                <label className={label}>Descrição</label>
                <input value={formMotivo.nome} onChange={e => setFormMotivo(f => ({ ...f, nome: e.target.value }))}
                  placeholder="Ex: Falta de matéria-prima..." className={inputBase} />
              </div>
              <div>
                <label className={label}>Tipo</label>
                <div className="grid grid-cols-2 gap-2">
                  <button onClick={() => setFormMotivo(f => ({ ...f, tipo: 'Interna' }))} className={
                    formMotivo.tipo === 'Interna'
                      ? 'h-8 text-xs font-medium border bg-blue-600 text-white border-blue-600 transition-colors'
                      : 'h-8 text-xs font-medium border border-zinc-200 dark:border-zinc-700 text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800 transition-colors'
                  }>
                    Interna
                  </button>
                  <button onClick={() => setFormMotivo(f => ({ ...f, tipo: 'Externa' }))} className={
                    formMotivo.tipo === 'Externa'
                      ? 'h-8 text-xs font-medium border bg-amber-500 text-white border-amber-500 transition-colors'
                      : 'h-8 text-xs font-medium border border-zinc-200 dark:border-zinc-700 text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800 transition-colors'
                  }>
                    Externa
                  </button>
                </div>
              </div>
            </div>
            <div className="grid grid-cols-2 gap-2">
              <button onClick={() => setModalMotivoOpen(false)} className={btnSecondarySm}>Cancelar</button>
              <button onClick={adicionarMotivoLocal} disabled={!formMotivo.nome} className={btnPrimary}>
                Adicionar
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}