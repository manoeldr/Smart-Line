// Modal exibido ao clicar em "Marcha" durante uma parada, para selecionar o motivo.
// Permite cadastrar um novo motivo (Interno/Externo) inline, sem sair da tela de Medição.
import { useState } from 'react'
import type { MotivoParadaDto } from '../services/maquinaService'
import { btnPrimary, btnSecondarySm, btnToggle } from '../styles/buttons'
import { inputBase, label } from '../styles/inputs'
import { modalOverlay, modalContainerMd, modalTitle, modalSubtitle } from '../styles/modals'

interface Props {
  open: boolean
  motivos: MotivoParadaDto[]
  loading: boolean
  onConfirmar: (motivoId: string) => void
  onCadastrarNovo: (nome: string, tipo: 'Interna' | 'Externa') => Promise<string>
  onCancelar: () => void
}

export default function MotivoParadaModal({ open, motivos, loading, onConfirmar, onCadastrarNovo, onCancelar }: Props) {
  const [selecionado, setSelecionado] = useState<string | null>(null)
  const [filtro, setFiltro] = useState<'todos' | 'Interna' | 'Externa'>('todos')
  const [cadastrando, setCadastrando] = useState(false)
  const [novoNome, setNovoNome] = useState('')
  const [novoTipo, setNovoTipo] = useState<'Interna' | 'Externa'>('Interna')
  const [salvando, setSalvando] = useState(false)

  if (!open) return null

  // Motivos planejados não aparecem aqui — são exclusivos do fluxo de Pausar medição
  const filtrados = motivos.filter(m =>
    m.tipo !== 'Planejada' && (filtro === 'todos' || m.tipo === filtro)
  )

  const tipoLabel: Record<string, string> = {
    Interna: 'interna',
    Externa: 'externa',
  }

  const tipoCor: Record<string, string> = {
    Interna: 'bg-blue-50 dark:bg-blue-950 text-blue-600 dark:text-blue-400',
    Externa: 'bg-amber-50 dark:bg-amber-950 text-amber-700 dark:text-amber-400',
  }

  function handleConfirmar() {
    if (!selecionado) return
    setSelecionado(null)
    setCadastrando(false)
    setNovoNome('')
    onConfirmar(selecionado)
  }

  async function handleSalvarNovo() {
    if (!novoNome.trim()) return
    setSalvando(true)
    try {
      const novoId = await onCadastrarNovo(novoNome.trim(), novoTipo)
      setSelecionado(novoId)
      setCadastrando(false)
      setNovoNome('')
    } finally {
      setSalvando(false)
    }
  }

  return (
    <div className={modalOverlay}>
      <div className={`${modalContainerMd} w-80`}>

        {/* Header */}
        <div className="mb-4 flex items-start justify-between">
          <div>
            <p className={modalTitle}>Motivo da parada</p>
            <p className={modalSubtitle}>Selecione o motivo que causou a parada</p>
          </div>
          <button onClick={onCancelar} className="text-zinc-400 hover:text-zinc-900 dark:hover:text-zinc-100">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
          </button>
        </div>

        {!cadastrando ? (
          <>
            {/* Filtro por tipo */}
            <div className="flex gap-1.5 mb-3">
              {(['todos', 'Interna', 'Externa'] as const).map(t => (
                <button
                  key={t}
                  onClick={() => setFiltro(t)}
                  className={`text-[10px] px-2.5 py-1 rounded border transition-colors ${
                    filtro === t
                      ? 'bg-blue-600 text-white border-blue-600'
                      : 'border-zinc-200 dark:border-zinc-700 text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800'
                  }`}
                >
                  {t === 'todos' ? 'Todos' : t === 'Interna' ? 'Internas' : 'Externas'}
                </button>
              ))}
            </div>

            {/* Lista de motivos */}
            <div className="flex flex-col gap-1 max-h-52 overflow-y-auto mb-3">
              {loading ? (
                <p className="text-xs text-zinc-400 text-center py-4">Carregando...</p>
              ) : filtrados.length === 0 ? (
                <p className="text-xs text-zinc-400 text-center py-4">Nenhum motivo cadastrado</p>
              ) : (
                filtrados.map(m => (
                  <button
                    key={m.id}
                    onClick={() => setSelecionado(m.id)}
                    className={`flex items-center justify-between px-3 py-2 rounded border text-left transition-colors ${
                      selecionado === m.id
                        ? 'border-blue-500 bg-blue-50 dark:bg-blue-950'
                        : 'border-zinc-100 dark:border-zinc-800 hover:bg-zinc-50 dark:hover:bg-zinc-800'
                    }`}
                  >
                    <span className="text-xs text-zinc-900 dark:text-zinc-100">{m.nome}</span>
                    <span className={`text-[10px] px-1.5 py-0.5 rounded flex-shrink-0 ml-2 ${tipoCor[m.tipo] ?? ''}`}>
                      {tipoLabel[m.tipo] ?? m.tipo}
                    </span>
                  </button>
                ))
              )}
            </div>

            {/* Cadastrar novo */}
            <button
              onClick={() => setCadastrando(true)}
              className="w-full flex items-center justify-center gap-1.5 py-1.5 mb-3 text-xs text-zinc-500 border border-dashed border-zinc-300 dark:border-zinc-700 rounded hover:bg-zinc-50 dark:hover:bg-zinc-800 transition-colors"
            >
              <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/>
              </svg>
              Cadastrar novo motivo
            </button>

            {/* Confirmar */}
            <button onClick={handleConfirmar} disabled={!selecionado} className={`w-full h-9 ${btnPrimary}`}>
              Confirmar
            </button>
          </>
        ) : (
          <>
            {/* Formulário novo motivo */}
            <div className="flex flex-col gap-3 mb-4">
              <div className="flex flex-col gap-1.5">
                <label className={label}>Descrição do motivo</label>
                <input
                  type="text"
                  value={novoNome}
                  onChange={e => setNovoNome(e.target.value)}
                  placeholder="Ex: Falta de matéria-prima"
                  autoFocus
                  className={inputBase.replace('h-8', 'h-9')}
                />
              </div>

              <div className="flex flex-col gap-1.5">
                <label className={label}>Tipo</label>
                <div className="grid grid-cols-2 gap-2">
                  <button onClick={() => setNovoTipo('Interna')} className={btnToggle(novoTipo === 'Interna', 'blue')}>
                    Interna
                  </button>
                  <button onClick={() => setNovoTipo('Externa')} className={btnToggle(novoTipo === 'Externa', 'amber')}>
                    Externa
                  </button>
                </div>
              </div>
            </div>

            <div className="grid grid-cols-2 gap-2">
              <button onClick={() => { setCadastrando(false); setNovoNome('') }} className={btnSecondarySm}>
                Cancelar
              </button>
              <button onClick={handleSalvarNovo} disabled={!novoNome.trim() || salvando} className={`${btnPrimary} h-9`}>
                {salvando ? 'Salvando...' : 'Salvar'}
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  )
}