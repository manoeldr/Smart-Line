import { useEffect, useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { linhaService } from '../../services/linhaService'
import { configuracaoService, type CampoMaquinaDto } from '../../services/configuracaoService'
import type { Linha, MaquinaLinha } from '../../types'

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

  // Campos do modal
  const [velocidadeNominal, setVelocidadeNominal] = useState('')
  const [sobreVelocidade, setSobreVelocidade] = useState('0')
  const [previsaoTermino, setPrevisaoTermino] = useState('')
  const [tipoColeta, setTipoColeta] = useState<TipoColeta>('Manual')
  const [producaoInicial, setProducaoInicial] = useState('')

  // Campos de coleta extras
  const [camposDisponiveis, setCamposDisponiveis] = useState<CampoMaquinaDto[]>([])
  const [loadingCampos, setLoadingCampos] = useState(false)
  const [camposSelecionados, setCamposSelecionados] = useState<Set<string>>(new Set())

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
      setVelocidadeNominal(String(maquina.velocidadeNominal ?? ''))
      setSobreVelocidade('0')
      setPrevisaoTermino('')
      setProducaoInicial('')
      setTipoColeta('Manual')
      setCamposSelecionados(new Set())

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
      if (novo.has(campoId)) novo.delete(campoId)
      else novo.add(campoId)
      return novo
    })
  }

  function handleConfirmar() {
    if (!maquinaSelecionada || !linhaSelecionada) return

    let previsaoISO: string | null = null
    if (previsaoTermino) {
      const hoje = new Date()
      const [hh, mm] = previsaoTermino.split(':').map(Number)
      const dt = new Date(hoje.getFullYear(), hoje.getMonth(), hoje.getDate(), hh, mm, 0)
      if (dt <= new Date()) dt.setDate(dt.getDate() + 1)
      previsaoISO = dt.toISOString()
    }

    onIniciar(
      maquinaSelecionada,
      linhaSelecionada,
      { producao: Number(producaoInicial) || 0 },
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

  const inputCls = 'w-full h-9 px-3 border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-sm text-zinc-900 dark:text-zinc-100 focus:outline-none focus:ring-1 focus:ring-blue-500'

  return (
    <>
      <div className="max-w-lg mx-auto mt-8">
        <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 p-5">
          <h2 className="text-sm font-medium text-zinc-900 dark:text-zinc-100 mb-4">Nova medição</h2>

          {/* Linha */}
          <div className="flex flex-col gap-1.5 mb-3">
            <label className="text-xs text-zinc-500">Linha de produção</label>
            <select
              value={linhaSelecionada?.id ?? ''}
              onChange={e => handleLinhaChange(e.target.value)}
              disabled={loadingLinhas}
              className="h-9 px-3 border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-sm text-zinc-900 dark:text-zinc-100 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50"
            >
              <option value="">{loadingLinhas ? 'Carregando...' : 'Selecionar linha...'}</option>
              {linhas.map(l => (
                <option key={l.id} value={l.id}>{l.nome}</option>
              ))}
            </select>
          </div>

          {/* Máquina */}
          <div className="flex flex-col gap-1.5 mb-4">
            <label className="text-xs text-zinc-500">Máquina</label>
            <select
              value={maquinaSelecionada?.id ?? ''}
              onChange={e => handleMaquinaChange(e.target.value)}
              disabled={!linhaSelecionada}
              className="h-9 px-3 border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-sm text-zinc-900 dark:text-zinc-100 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50"
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

          <button
            onClick={handleAbrirModal}
            disabled={!podeAbrirModal}
            className="w-full h-9 bg-blue-600 hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed text-white text-sm font-medium transition-colors"
          >
            {loadingExterno ? 'Iniciando...' : 'Configurar medição'}
          </button>
        </div>
      </div>

      {/* Modal de configuração */}
      {modalOpen && maquinaSelecionada && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 w-[480px] max-h-[90vh] flex flex-col">

            {/* Header */}
            <div className="px-5 py-4 border-b border-zinc-200 dark:border-zinc-800">
              <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">Configurar medição</p>
              <p className="text-xs text-zinc-400 mt-0.5">{maquinaSelecionada.maquinaNome}</p>
            </div>

            <div className="px-5 py-4 flex flex-col gap-4 overflow-y-auto">

              {/* Forma de medição */}
              <div>
                <label className="text-xs text-zinc-500 mb-2 block">Forma de medição</label>
                <div className="grid grid-cols-3 gap-2">
                  {(['Manual', 'SemiAutomatico', 'Automatico'] as TipoColeta[]).map(t => (
                    <button
                      key={t}
                      onClick={() => setTipoColeta(t)}
                      disabled={t !== 'Manual'}
                      className={`h-9 text-xs font-medium border transition-colors disabled:opacity-40 disabled:cursor-not-allowed ${
                        tipoColeta === t
                          ? 'bg-blue-600 text-white border-blue-600'
                          : 'border-zinc-200 dark:border-zinc-700 text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800'
                      }`}
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
                  <label className="text-xs text-zinc-500 mb-1 block">Velocidade nominal (prod/h)</label>
                  <input
                    type="number" min="0"
                    value={velocidadeNominal}
                    onChange={e => setVelocidadeNominal(e.target.value)}
                    placeholder="Ex: 1200"
                    className={inputCls}
                  />
                </div>
                <div>
                  <label className="text-xs text-zinc-500 mb-1 block">Sobre velocidade (%)</label>
                  <input
                    type="number" min="0" max="100"
                    value={sobreVelocidade}
                    onChange={e => setSobreVelocidade(e.target.value)}
                    placeholder="Ex: 10"
                    className={inputCls}
                  />
                </div>
              </div>

              {/* Produção inicial */}
              <div>
                <label className="text-xs text-zinc-500 mb-1 block">Produção até então</label>
                <input
                  type="number" min="0"
                  value={producaoInicial}
                  onChange={e => setProducaoInicial(e.target.value)}
                  placeholder="Leitura atual do contador"
                  className={inputCls}
                />
              </div>

              {/* Previsão de término */}
              <div>
                <label className="text-xs text-zinc-500 mb-1 block">Previsão de término</label>
                <input
                  type="time"
                  value={previsaoTermino}
                  onChange={e => setPrevisaoTermino(e.target.value)}
                  className={inputCls}
                />
                <p className="text-[10px] text-zinc-400 mt-1">
                  Ao atingir este horário, a medição será finalizada automaticamente após 5 minutos
                </p>
              </div>

              {/* Campos a coletar */}
              <div>
                <label className="text-xs text-zinc-500 mb-2 block">Campos a coletar</label>

                {/* Produção fixo */}
                <div className="flex items-center gap-2 py-1.5">
                  <input type="checkbox" checked disabled className="accent-blue-600" />
                  <span className="text-xs text-zinc-900 dark:text-zinc-100">Produção</span>
                  <span className="text-[10px] text-zinc-400">(sempre coletado)</span>
                </div>

                {loadingCampos ? (
                  <p className="text-xs text-zinc-400 mt-1">Carregando campos...</p>
                ) : camposDisponiveis.length === 0 ? (
                  <p className="text-xs text-zinc-400 mt-1">Nenhum campo extra cadastrado para esta máquina</p>
                ) : (
                  camposDisponiveis.map(c => (
                    <label key={c.id} className="flex items-center gap-2 py-1.5 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={camposSelecionados.has(c.id)}
                        onChange={() => toggleCampo(c.id)}
                        className="accent-blue-600"
                      />
                      <span className="text-xs text-zinc-900 dark:text-zinc-100">
                        {c.nome} {c.unidade && <span className="text-zinc-400">({c.unidade})</span>}
                      </span>
                    </label>
                  ))
                )}
              </div>
            </div>

            {/* Footer */}
            <div className="px-5 py-3 border-t border-zinc-200 dark:border-zinc-800 flex justify-end gap-2">
              <button
                onClick={() => setModalOpen(false)}
                className="h-8 px-4 border border-zinc-200 dark:border-zinc-700 text-xs text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800"
              >
                Cancelar
              </button>
              <button
                onClick={handleConfirmar}
                disabled={!podeConfirmar || !!loadingExterno}
                className="h-8 px-4 bg-blue-600 hover:bg-blue-700 disabled:opacity-40 text-white text-xs font-medium"
              >
                {loadingExterno ? 'Iniciando...' : 'Iniciar medição'}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}