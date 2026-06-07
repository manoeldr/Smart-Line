import { useEffect, useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { linhaService } from '../../services/linhaService'
import type { Linha, MaquinaLinha } from '../../types'

interface Props {
  onIniciar: (maquina: MaquinaLinha, linha: Linha, leiturasIniciais: Record<string, number>) => void
}

export default function SelecaoMaquina({ onIniciar }: Props) {
  const { clienteId } = useAuth()
  const [linhas, setLinhas] = useState<Linha[]>([])
  const [linhaSelecionada, setLinhaSelecionada] = useState<Linha | null>(null)
  const [maquinaSelecionada, setMaquinaSelecionada] = useState<MaquinaLinha | null>(null)
  const [leiturasIniciais, setLeiturasIniciais] = useState<Record<string, number>>({})
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    if (!clienteId) return
    async function carregar() {
      setLoading(true)
      try {
        const data = await linhaService.getLinhasByCliente(clienteId!)
        setLinhas(data)
      } finally {
        setLoading(false)
      }
    }
    carregar()
  }, [clienteId])

  function handleLinhaChange(linhaId: string) {
    const linha = linhas.find(l => l.id === linhaId) ?? null
    setLinhaSelecionada(linha)
    setMaquinaSelecionada(null)
    setLeiturasIniciais({})
  }

  function handleMaquinaChange(maquinaId: string) {
    const maquina = linhaSelecionada?.maquinas.find(m => m.id === maquinaId) ?? null
    setMaquinaSelecionada(maquina)
    setLeiturasIniciais({})
  }

  function handleLeituraInicial(tipoId: string, valor: string) {
    setLeiturasIniciais(prev => ({ ...prev, [tipoId]: Number(valor) }))
  }

  function handleIniciar() {
    if (!maquinaSelecionada || !linhaSelecionada) return
    onIniciar(maquinaSelecionada, linhaSelecionada, leiturasIniciais)
  }

  const podeIniciar = !!maquinaSelecionada && !maquinaSelecionada.sessaoAtiva

  return (
    <div className="max-w-lg mx-auto mt-8">
      <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 rounded-xl p-5">
        <h2 className="text-sm font-medium text-zinc-900 dark:text-zinc-100 mb-4">Nova medição</h2>

        {/* Linha */}
        <div className="flex flex-col gap-1.5 mb-3">
          <label className="text-xs text-zinc-500">Linha de produção</label>
          <select
            value={linhaSelecionada?.id ?? ''}
            onChange={e => handleLinhaChange(e.target.value)}
            className="h-9 px-3 rounded-sm border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-sm text-zinc-900 dark:text-zinc-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="">Selecionar linha...</option>
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
            className="h-9 px-3 rounded-sm border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-sm text-zinc-900 dark:text-zinc-100 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50"
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

        {/* Leituras iniciais */}
        {maquinaSelecionada && !maquinaSelecionada.sessaoAtiva && (
          <div className="border-t border-zinc-100 dark:border-zinc-800 pt-4 mb-4">
            <p className="text-xs text-zinc-500 mb-3">Leitura inicial do contador</p>
            <div className="grid grid-cols-2 gap-3">
              {/* Produção sempre aparece */}
              <div className="flex flex-col gap-1">
                <label className="text-[10px] text-zinc-400">Produção</label>
                <input
                  type="number"
                  min="0"
                  placeholder="0"
                  value={leiturasIniciais['producao'] ?? ''}
                  onChange={e => handleLeituraInicial('producao', e.target.value)}
                  className="h-8 px-3 rounded-sm border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-sm text-zinc-900 dark:text-zinc-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
              </div>
            </div>
          </div>
        )}

        {/* Sessão ativa */}
        {maquinaSelecionada?.sessaoAtiva && (
          <div className="bg-amber-50 dark:bg-amber-950 border border-amber-200 dark:border-amber-800 rounded-sm px-3 py-2 mb-4 text-xs text-amber-700 dark:text-amber-400">
            Esta máquina já possui uma sessão ativa no momento.
          </div>
        )}

        <button
          onClick={handleIniciar}
          disabled={!podeIniciar || loading}
          className="w-full h-9 bg-blue-600 hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed text-white text-sm font-medium rounded-sm transition-colors"
        >
          {loading ? 'Carregando...' : 'Iniciar medição'}
        </button>
      </div>
    </div>
  )
}
