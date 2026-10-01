// Tela de Dashboard — seleciona uma linha e um período de datas,
// mostra cards com OEE agregado de cada máquina (sessões finalizadas e em andamento; as em
// andamento aparecem "ao vivo" com a situação agora). Atualiza sozinha a cada 5 min.
// Clicar num card abre o modal de detalhes. A visão "Linha" junta a linha inteira: OEE pela
// máquina crítica e as paradas somadas de todas as máquinas.
import { useEffect, useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { linhaService } from '../../services/linhaService'
import { dashboardService, type LinhaDashboardDto, type MaquinaDashboardDto } from '../../services/dashboardService'
import type { Linha } from '../../types'
import MaquinaDashboardCard from './MaquinaDashboardCard'
import LinhaGeral from './LinhaGeral'
import MaquinaDetalheModal from '../../modals/MaquinaDetalheModal'
import { useParametroUrl } from '../../hooks/useParametroUrl'
import { inputMd } from '../../styles/inputs'
import { cardPadded } from '../../styles/cards'
import CampoData from '../../components/CampoData'
import { clienteService } from '../../services/clienteService'

// A coleta Semi Automática grava a produção nos múltiplos de 5 min do relógio (10:00, 10:05...):
// a tela recarrega logo depois (10 s de folga).
const INTERVALO_GRAVACAO_MS = 5 * 60 * 1000
const FOLGA_MS = 10 * 1000

function msAteProximaAtualizacao(agora = Date.now()) {
  const proxima = Math.ceil((agora - FOLGA_MS) / INTERVALO_GRAVACAO_MS) * INTERVALO_GRAVACAO_MS + FOLGA_MS
  return Math.max(1000, proxima - agora)
}

type Visao = 'maquinas' | 'linha'

const OPCOES_VISAO: { valor: Visao; rotulo: string }[] = [
  { valor: 'maquinas', rotulo: 'Máquinas' },
  { valor: 'linha', rotulo: 'Linha' },
]

// A visão escolhida fica guardada neste navegador (F5 volta nela).
const CHAVE_VISAO = 'smartline.dashboard.visao'

function visaoGuardada(): Visao {
  try {
    return localStorage.getItem(CHAVE_VISAO) === 'linha' ? 'linha' : 'maquinas'
  } catch {
    return 'maquinas'
  }
}

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

  const [visao, setVisao] = useState<Visao>(visaoGuardada)
  // Dias com sessão na linha escolhida: bolinha azul nos calendários de início e fim
  const [datasComSessao, setDatasComSessao] = useState<string[]>([])
  const [dados, setDados] = useState<MaquinaDashboardDto[]>([])
  const [linhaGeral, setLinhaGeral] = useState<LinhaDashboardDto | null>(null)
  const [loadingDados, setLoadingDados] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [atualizadoEm, setAtualizadoEm] = useState<Date | null>(null)

  // Máquina com o detalhe aberto: fica na URL (?maquina=) para o F5 reabrir o modal.
  const [maquinaLinhaSelecionada, setMaquinaLinhaSelecionada] = useParametroUrl('maquina')

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

  // Dias com sessão da linha escolhida, para os calendários (uma consulta ao trocar de linha).
  useEffect(() => {
    if (!clienteId || !linhaSelecionada) return
    let ativo = true
    clienteService.getDatasComSessao(clienteId, linhaSelecionada)
      .then(datas => { if (ativo) setDatasComSessao(datas) })
      .catch(() => { if (ativo) setDatasComSessao([]) })
    return () => { ativo = false }
  }, [clienteId, linhaSelecionada])

  // "Carregando..." só ao trocar linha, período ou visão; depois disso a tela se atualiza sozinha,
  // no lugar, logo depois de cada gravação da produção do Semi Automático (a cada 5 min).
  useEffect(() => {
    if (!linhaSelecionada) return
    let ativo = true
    let timer: ReturnType<typeof setTimeout> | undefined
    let primeira = true

    async function carregarDados() {
      if (primeira) {
        setLoadingDados(true)
        setErro(null)
      }
      try {
        const inicioIso = new Date(dataInicio + 'T00:00:00').toISOString()
        const fimIso = new Date(dataFim + 'T23:59:59').toISOString()
        if (visao === 'linha') {
          const data = await dashboardService.getLinhaGeral(linhaSelecionada, inicioIso, fimIso)
          if (!ativo) return
          setLinhaGeral(data)
        } else {
          const data = await dashboardService.getDashboardLinha(linhaSelecionada, inicioIso, fimIso)
          if (!ativo) return
          setDados(data)
        }
        setErro(null)
        setAtualizadoEm(new Date())
      } catch (e: unknown) {
        // Numa atualização em segundo plano, uma falha só mantém o que já está na tela.
        if (ativo && primeira) setErro(e instanceof Error ? e.message : 'Erro ao carregar dashboard')
      } finally {
        if (ativo) {
          if (primeira) setLoadingDados(false)
          primeira = false
          timer = setTimeout(carregarDados, msAteProximaAtualizacao())
        }
      }
    }
    carregarDados()
    return () => { ativo = false; clearTimeout(timer) }
  }, [linhaSelecionada, dataInicio, dataFim, visao])

  function escolherVisao(v: Visao) {
    setVisao(v)
    try { localStorage.setItem(CHAVE_VISAO, v) } catch { /* sem armazenamento: só não lembra */ }
  }

  function abrirDetalhe(maquinaLinhaId: string) {
    setMaquinaLinhaSelecionada(maquinaLinhaId)
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
          <CampoData valor={dataInicio} onChange={setDataInicio} datasComSessao={datasComSessao} max={dataFim} />
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-xs text-zinc-500">Data fim</label>
          <CampoData valor={dataFim} onChange={setDataFim} datasComSessao={datasComSessao} min={dataInicio} />
        </div>

        <div className="flex flex-col gap-1.5">
          <label className="text-xs text-zinc-500">Visão</label>
          <div className="flex">
            {OPCOES_VISAO.map(o => (
              <button
                key={o.valor}
                onClick={() => escolherVisao(o.valor)}
                className={`h-9 w-24 -ml-px first:ml-0 text-center text-xs font-medium border transition-colors ${
                  visao === o.valor
                    ? 'relative bg-blue-600 text-white border-blue-600'
                    : 'border-zinc-200 dark:border-zinc-700 text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800'
                }`}
              >
                {o.rotulo}
              </button>
            ))}
          </div>
        </div>

        {atualizadoEm && (
          <p className="ml-auto text-[10px] text-zinc-400 self-center">
            Atualiza sozinho a cada 5 min · última às {atualizadoEm.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}
          </p>
        )}
      </div>

      {/* Grid de cards ou a visão geral da linha */}
      {loadingDados ? (
        <div className="flex items-center justify-center h-48 text-sm text-zinc-400">Carregando...</div>
      ) : erro ? (
        <div className="flex items-center justify-center h-48 text-sm text-red-400">Erro: {erro}</div>
      ) : visao === 'linha' ? (
        linhaGeral && linhaGeral.maquinas.length > 0
          ? <LinhaGeral dados={linhaGeral} onAbrirMaquina={abrirDetalhe} />
          : <div className="flex items-center justify-center h-48 text-sm text-zinc-400">Nenhuma máquina nesta linha</div>
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
        open={maquinaLinhaSelecionada !== null}
        maquinaLinhaId={maquinaLinhaSelecionada}
        onFechar={() => setMaquinaLinhaSelecionada(null)}
      />
    </div>
  )
}