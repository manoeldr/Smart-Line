// Tela inicial da Medição: seleciona linha e máquina, depois abre o modal de configuração
// da sessão (forma de coleta, velocidade nominal/sobrevelocidade herdadas da linha,
// produção até então, campos extras a coletar, valores iniciais e previsão de término).
import { useEffect, useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { linhaService } from '../../services/linhaService'
import { configuracaoService, type CampoMaquinaDto } from '../../services/configuracaoService'
import type { Linha, MaquinaLinha } from '../../types'
import Switch from '../../components/Switch'
import { btnPrimary, btnSecondarySm, btnToggle } from '../../styles/buttons'
import { inputMdFull, label } from '../../styles/inputs'
import { modalOverlay, modalPanel, modalHeader, modalTitle, modalSubtitle, modalBody, modalFooter } from '../../styles/modals'
import { cardPadded } from '../../styles/cards'

interface Props {
  onIniciar: (
    maquina: MaquinaLinha,
    linha: Linha,
    leiturasIniciais: Record<string, number>,
    params: {
      velocidadeNominal: number
      sobreVelocidade: number
      previsaoTermino: string | null
      tipoColeta: string
      campoMaquinaIds: string[]
    }
  ) => void
  loading?: boolean
}

type TipoColeta = 'Manual' | 'SemiAutomatico' | 'Automatico'

export default function SelecaoMaquina({ onIniciar, loading: loadingExterno }: Props) {
  const { clienteId } = useAuth()
  const [linhas, setLinhas] = useState<Linha[]>([])
  const [linhaSelecionada, setLinhaSelecionada] = useState<Linha | null>(null)
  const [maquinaSelecionada, setMaquinaSelecionada] = useState<MaquinaLinha | null>(null)
  const [loadingLinhas, setLoadingLinhas] = useState(false)
  const [modalOpen, setModalOpen] = useState(false)

  // Campos do modal de configuração
  const [velocidadeNominal, setVelocidadeNominal] = useState('')
  const [sobreVelocidade, setSobreVelocidade] = useState('0')
  const [previsaoTermino, setPrevisaoTermino] = useState('')
  const [tipoColeta, setTipoColeta] = useState<TipoColeta>('Manual')
  const [producaoInicial, setProducaoInicial] = useState('')

  // Campos de coleta extras da máquina
  const [camposDisponiveis, setCamposDisponiveis] = useState<CampoMaquinaDto[]>([])
  const [loadingCampos, setLoadingCampos] = useState(false)
  const [camposSelecionados, setCamposSelecionados] = useState<Set<string>>(new Set())
  const [valoresIniciaisExtras, setValoresIniciaisExtras] = useState<Record<string, string>>({})

  useEffect(() => {
    if (!clienteId) return
    async function carregar() {
      setLoadingLinhas(true)
      try {
        const data = await linhaService.getLinhasByCliente(clienteId!)
        setLinhas(data)
      } finally {
        setLoadingLinhas(false)
      }
    }
    carregar()
  }, [clienteId])

  function handleLinhaChange(linhaId: string) {
    const linha = linhas.find(l => l.id === linhaId) ?? null
    setLinhaSelecionada(linha)
    setMaquinaSelecionada(null)
  }

  async function handleMaquinaChange(maquinaId: string) {
    const maquina = linhaSelecionada?.maquinas.find(m => m.id === maquinaId) ?? null
    setMaquinaSelecionada(maquina)
    if (maquina) {
      // Velocidade nominal vem pré-preenchida da configuração da linha, mas é editável aqui
      setVelocidadeNominal(String(maquina.velocidadeNominal ?? ''))
      setSobreVelocidade('0')
      setPrevisaoTermino('')
      setProducaoInicial('')
      setTipoColeta('Manual')
      setCamposSelecionados(new Set())
      setValoresIniciaisExtras({})

      setLoadingCampos(true)
      try {
        const campos = await configuracaoService.getCamposMaquina(maquina.maquinaId)
        setCamposDisponiveis(campos.filter(c => c.ativo))
      } finally {
        setLoadingCampos(false)
      }
    }
  }

  function handleAbrirModal() {
    setModalOpen(true)
  }

  function toggleCampo(campoId: string) {
    setCamposSelecionados(prev => {
      const novo = new Set(prev)
      if (novo.has(campoId)) {
        novo.delete(campoId)
        setValoresIniciaisExtras(v => {
          const copia = { ...v }
          delete copia[campoId]
          return copia
        })
      } else {
        novo.add(campoId)
      }
      return novo
    })
  }

  function handleValorInicialExtra(campoId: string, valor: string) {
    setValoresIniciaisExtras(prev => ({ ...prev, [campoId]: valor }))
  }

  function handleConfirmar() {
    if (!maquinaSelecionada || !linhaSelecionada) return

    // Converte a hora escolhida (HH:MM) para um ISO completo. Se já passou hoje, assume amanhã.
    let previsaoISO: string | null = null
    if (previsaoTermino) {
      const hoje = new Date()
      const [hh, mm] = previsaoTermino.split(':').map(Number)
      const dt = new Date(hoje.getFullYear(), hoje.getMonth(), hoje.getDate(), hh, mm, 0)
      if (dt <= new Date()) dt.setDate(dt.getDate() + 1)
      previsaoISO = dt.toISOString()
    }

    const leiturasIniciais: Record<string, number> = {
      producao: Number(producaoInicial) || 0,
    }
    for (const campoId of camposSelecionados) {
      leiturasIniciais[campoId] = Number(valoresIniciaisExtras[campoId]) || 0
    }

    onIniciar(
      maquinaSelecionada,
      linhaSelecionada,
      leiturasIniciais,
      {
        velocidadeNominal: Number(velocidadeNominal) || 0,
        sobreVelocidade: Number(sobreVelocidade) || 0,
        previsaoTermino: previsaoISO,
        tipoColeta,
        campoMaquinaIds: Array.from(camposSelecionados),
      }
    )
    setModalOpen(false)
  }

  const podeAbrirModal = !!maquinaSelecionada && !maquinaSelecionada.sessaoAtiva && !loadingExterno
  const podeConfirmar = !!velocidadeNominal && Number(velocidadeNominal) > 0

  return (
    <>
      <div className="max-w-lg mx-auto mt-8">
        <div className={cardPadded}>
          <h2 className="text-sm font-medium text-zinc-900 dark:text-zinc-100 mb-4">Nova medição</h2>

          {/* Linha */}
          <div className="flex flex-col gap-1.5 mb-3">
            <label className={label}>Linha de produção</label>
            <select
              value={linhaSelecionada?.id ?? ''}
              onChange={e => handleLinhaChange(e.target.value)}
              disabled={loadingLinhas}
              className={inputMdFull}
            >
              <option value="">{loadingLinhas ? 'Carregando...' : 'Selecionar linha...'}</option>
              {linhas.map(l => (
                <option key={l.id} value={l.id}>{l.nome}</option>
              ))}
            </select>
          </div>

          {/* Máquina */}
          <div className="flex flex-col gap-1.5 mb-4">
            <label className={label}>Máquina</label>
            <select
              value={maquinaSelecionada?.id ?? ''}
              onChange={e => handleMaquinaChange(e.target.value)}
              disabled={!linhaSelecionada}
              className={inputMdFull}
            >
              <option value="">Selecionar máquina...</option>
              {linhaSelecionada?.maquinas
                .sort((a, b) => a.ordem - b.ordem)
                .map(m => (
                  <option key={m.id} value={m.id} disabled={m.sessaoAtiva}>
                    {m.maquinaNome}{m.sessaoAtiva ? ' (sessão ativa)' : ''}{m.critica ? ' ★' : ''}
                  </option>
                ))}
            </select>
          </div>

          {/* Sessão ativa */}
          {maquinaSelecionada?.sessaoAtiva && (
            <div className="bg-amber-50 dark:bg-amber-950 border border-amber-200 dark:border-amber-800 px-3 py-2 mb-4 text-xs text-amber-700 dark:text-amber-400">
              Esta máquina já possui uma sessão ativa no momento.
            </div>
          )}

          <button onClick={handleAbrirModal} disabled={!podeAbrirModal} className={`w-full ${btnPrimary}`}>
            {loadingExterno ? 'Iniciando...' : 'Configurar medição'}
          </button>
        </div>
      </div>

      {/* Modal de configuração */}
      {modalOpen && maquinaSelecionada && (
        <div className={modalOverlay}>
          <div className={`${modalPanel} w-[480px] max-h-[90vh]`}>

            <div className={modalHeader}>
              <p className={modalTitle}>Configurar medição</p>
              <p className={modalSubtitle}>{maquinaSelecionada.maquinaNome}</p>
            </div>

            <div className={modalBody}>

              {/* Forma de medição */}
              <div>
                <label className="text-xs text-zinc-500 mb-2 block">Forma de medição</label>
                <div className="grid grid-cols-3 gap-2">
                  {(['Manual', 'SemiAutomatico', 'Automatico'] as TipoColeta[]).map(t => (
                    <button
                      key={t}
                      onClick={() => setTipoColeta(t)}
                      disabled={t !== 'Manual'}
                      className={btnToggle(tipoColeta === t, 'blue')}
                    >
                      {t === 'Manual' ? 'Manual' : t === 'SemiAutomatico' ? 'Semi Auto' : 'Automático'}
                    </button>
                  ))}
                </div>
                {tipoColeta !== 'Manual' && (
                  <p className="text-[10px] text-amber-600 dark:text-amber-400 mt-1">Em desenvolvimento</p>
                )}
              </div>

              {/* Velocidade */}
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className={label}>Velocidade nominal (prod/h)</label>
                  <input
                    type="number" min="0"
                    value={velocidadeNominal}
                    onChange={e => setVelocidadeNominal(e.target.value)}
                    placeholder="Ex: 1200"
                    className={inputMdFull}
                  />
                </div>
                <div>
                  <label className={label}>Sobre velocidade (%)</label>
                  <input
                    type="number" min="0" max="100"
                    value={sobreVelocidade}
                    onChange={e => setSobreVelocidade(e.target.value)}
                    placeholder="Ex: 10"
                    className={inputMdFull}
                  />
                </div>
              </div>

              {/* Produção inicial */}
              <div>
                <label className={label}>Produção até então</label>
                <input
                  type="number" min="0"
                  value={producaoInicial}
                  onChange={e => setProducaoInicial(e.target.value)}
                  placeholder="Leitura atual do contador"
                  className={inputMdFull}
                />
              </div>

              {/* Campos a coletar — escolha primeiro o quê medir */}
              <div>
                <label className="text-xs text-zinc-500 mb-2 block">Campos a coletar</label>

                {/* Produção fixo */}
                <div className="flex items-center justify-between gap-2 py-1.5">
                  <span className="text-xs text-zinc-900 dark:text-zinc-100">
                    Produção <span className="text-[10px] text-zinc-400">(sempre coletado)</span>
                  </span>
                  <Switch checked disabled />
                </div>

                {loadingCampos ? (
                  <p className="text-xs text-zinc-400 mt-1">Carregando campos...</p>
                ) : camposDisponiveis.length === 0 ? (
                  <p className="text-xs text-zinc-400 mt-1">Nenhum campo extra cadastrado para esta máquina</p>
                ) : (
                  camposDisponiveis.map(c => (
                    <div key={c.id} className="flex items-center justify-between gap-2 py-1.5">
                      <span className="text-xs text-zinc-900 dark:text-zinc-100">
                        {c.nome} {c.unidade && <span className="text-zinc-400">({c.unidade})</span>}
                      </span>
                      <Switch checked={camposSelecionados.has(c.id)} onChange={() => toggleCampo(c.id)} />
                    </div>
                  ))
                )}
              </div>

              {/* Leituras iniciais dos campos extras selecionados — só depois de escolher o quê medir */}
              {camposDisponiveis
                .filter(c => camposSelecionados.has(c.id))
                .map(c => (
                  <div key={c.id}>
                    <label className={label}>
                      {c.nome} até então {c.unidade && `(${c.unidade})`}
                    </label>
                    <input
                      type="number"
                      value={valoresIniciaisExtras[c.id] ?? ''}
                      onChange={e => handleValorInicialExtra(c.id, e.target.value)}
                      placeholder="Leitura atual"
                      className={inputMdFull}
                    />
                  </div>
                ))}

              {/* Previsão de término */}
              <div>
                <label className={label}>Previsão de término</label>
                <input
                  type="time"
                  value={previsaoTermino}
                  onChange={e => setPrevisaoTermino(e.target.value)}
                  className={inputMdFull}
                />
                <p className="text-[10px] text-zinc-400 mt-1">
                  Ao atingir este horário, a medição será finalizada automaticamente após 5 minutos
                </p>
              </div>
            </div>

            <div className={modalFooter}>
              <button onClick={() => setModalOpen(false)} className={btnSecondarySm}>
                Cancelar
              </button>
              <button onClick={handleConfirmar} disabled={!podeConfirmar || !!loadingExterno} className={btnPrimary}>
                {loadingExterno ? 'Iniciando...' : 'Iniciar medição'}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}