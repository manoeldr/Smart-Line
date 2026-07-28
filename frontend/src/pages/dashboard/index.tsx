// Tela de Dashboard — seleciona uma linha e um período de datas,
// mostra cards com OEE agregado de cada máquina. Clicar num card abre o modal de detalhes.
import { useEffect, useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { linhaService } from '../../services/linhaService'
import { dashboardService, type MaquinaDashboardDto } from '../../services/dashboardService'
import type { Linha } from '../../types'
import MaquinaDashboardCard from './MaquinaDashboardCard'
import MaquinaDetalheModal from '../../modals/MaquinaDetalheModal'
import { inputMd } from '../../styles/inputs'
import { cardPadded } from '../../styles/cards'

function formatarDataInput(data: Date) {
  return data.toISOString().slice(0, 10)
}

export default function Dashboard() {
  const { clienteId } = useAuth()
  const [linhas, setLinhas] = useState<Linha[]>([])
  const [linhaSelecionada, setLinhaSelecionada] = useState<string>('')
  const [loadingLinhas, setLoadingLinhas] = useState(false)

  // Padrão: últimos 7 dias
  const hoje = new Date()
  const seteDiasAtras = new Date(hoje.getTime() - 7 * 24 * 60 * 60 * 1000)

  const [dataInicio, setDataInicio] = useState(formatarDataInput(seteDiasAtras))
  const [dataFim, setDataFim] = useState(formatarDataInput(hoje))

  const [dados, setDados] = useState<MaquinaDashboardDto[]>([])
  const [loadingDados, setLoadingDados] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  const [modalOpen, setModalOpen] = useState(false)
  const [maquinaLinhaSelecionada, setMaquinaLinhaSelecionada] = useState<string | null>(null)

  useEffect(() => {
    if (!clienteId) return
    async function carregar() {
      setLoadingLinhas(true)
      try {
        const data = await linhaService.getLinhasByCliente(clienteId!)
        setLinhas(data)
        if (data.length > 0) setLinhaSelecionada(data[0].id)
      } finally {
        setLoadingLinhas(false)
      }
    }
    carregar()
  }, [clienteId])

  useEffect(() => {
    if (!linhaSelecionada) return
    async function carregarDados() {
      setLoadingDados(true)
      setErro(null)
      try {
        const inicioIso = new Date(dataInicio + 'T00:00:00').toISOString()
        const fimIso = new Date(dataFim + 'T23:59:59').toISOString()
        const data = await dashboardService.getDashboardLinha(linhaSelecionada, inicioIso, fimIso)
        setDados(data)
      } catch (e: unknown) {
        setErro(e instanceof Error ? e.message : 'Erro ao carregar dashboard')
      } finally {
        setLoadingDados(false)
      }
    }
    carregarDados()
  }, [linhaSelecionada, dataInicio, dataFim])

  function abrirDetalhe(maquinaLinhaId: string) {
    setMaquinaLinhaSelecionada(maquinaLinhaId)
    setModalOpen(true)
  }

  return (
    <div className="p-4 flex flex-col gap-4">

      {/* Filtros */}
      <div className={`${cardPadded} flex items-end gap-3 flex-wrap`}>
        <div className="flex flex-col gap-1.5">
          <label className="text-xs text-zinc-500">Linha</label>
          <select
            value={linhaSelecionada}
            onChange={e => setLinhaSelecionada(e.target.value)}
            disabled={loadingLinhas}
            className={inputMd}
          >
            {linhas.map(l => (
              <option key={l.id} value={l.id}>{l.nome}</option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-xs text-zinc-500">Data início</label>
          <input type="date" value={dataInicio} onChange={e => setDataInicio(e.target.value)} className={inputMd} />
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-xs text-zinc-500">Data fim</label>
          <input type="date" value={dataFim} onChange={e => setDataFim(e.target.value)} className={inputMd} />
        </div>
      </div>

      {/* Grid de cards */}
      {loadingDados ? (
        <div className="flex items-center justify-center h-48 text-sm text-zinc-400">Carregando...</div>
      ) : erro ? (
        <div className="flex items-center justify-center h-48 text-sm text-red-400">Erro: {erro}</div>
      ) : dados.length === 0 ? (
        <div className="flex items-center justify-center h-48 text-sm text-zinc-400">Nenhuma máquina nesta linha</div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {dados.map(m => (
            <div key={m.maquinaLinhaId} onClick={() => abrirDetalhe(m.maquinaLinhaId)}>
              <MaquinaDashboardCard dados={m} />
            </div>
          ))}
        </div>
      )}

      <MaquinaDetalheModal
        open={modalOpen}
        maquinaLinhaId={maquinaLinhaSelecionada}
        onFechar={() => setModalOpen(false)}
      />
    </div>
  )
}