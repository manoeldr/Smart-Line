// Tela de Dashboard — seleciona uma linha; o dia vem do filtro de data do topo (sem filtro = hoje).
// Mostra cards com OEE agregado de cada máquina (sessões finalizadas e em andamento; as em
// andamento aparecem "ao vivo" com a situação agora). Atualiza sozinha a cada 5 min.
// Clicar num card abre o modal de detalhes. A visão "Linha" junta a linha inteira: OEE pela
// máquina crítica e as paradas somadas de todas as máquinas.
import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
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
import Switch from '../../components/Switch'

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

// "Só em medição" (máquinas com sessão em andamento) também fica guardado neste navegador.
const CHAVE_SO_EM_MEDICAO = 'smartline.dashboard.soEmMedicao'

function soEmMedicaoGuardado(): boolean {
  try {
    return localStorage.getItem(CHAVE_SO_EM_MEDICAO) === '1'
  } catch {
    return false
  }
}

// Hoje no fuso local (o mesmo critério do filtro do topo).
function hojeTexto() {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

export default function Dashboard() {
  const { clienteId } = useAuth()
  // Filtro de data do topo: nulo = hoje (ao vivo)
  const { dataFiltro } = useOutletContext<{ dataFiltro: string | null }>()
  const [linhas, setLinhas] = useState<Linha[]>([])
  const [linhaSelecionada, setLinhaSelecionada] = useState<string>('')
  const [loadingLinhas, setLoadingLinhas] = useState(false)

  // Um dia só: o do filtro do topo ou hoje.
  const data = dataFiltro ?? hojeTexto()

  const [visao, setVisao] = useState<Visao>(visaoGuardada)
  const [soEmMedicao, setSoEmMedicao] = useState<boolean>(soEmMedicaoGuardado)
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
        const inicioIso = new Date(data + 'T00:00:00').toISOString()
        const fimIso = new Date(data + 'T23:59:59').toISOString()
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
  }, [linhaSelecionada, data, visao])

  function escolherVisao(v: Visao) {
    setVisao(v)
    try { localStorage.setItem(CHAVE_VISAO, v) } catch { /* sem armazenamento: só não lembra */ }
  }

  function escolherSoEmMedicao(v: boolean) {
    setSoEmMedicao(v)
    try { localStorage.setItem(CHAVE_SO_EM_MEDICAO, v ? '1' : '0') } catch { /* sem armazenamento: só não lembra */ }
  }

  function abrirDetalhe(maquinaLinhaId: string) {
    setMaquinaLinhaSelecionada(maquinaLinhaId)
  }

  // "Só em medição" só faz sentido hoje (num dia passado nenhuma máquina está ao vivo).
  const filtrarEmMedicao = soEmMedicao && dataFiltro === null
  const dadosVisiveis = filtrarEmMedicao ? dados.filter(m => m.aoVivo) : dados
  // Na visão Linha, os indicadores da linha continuam os da linha inteira; o filtro vale para a
  // tabela e os gráficos por máquina.
  const linhaGeralVisivel = linhaGeral && filtrarEmMedicao
    ? { ...linhaGeral, maquinas: linhaGeral.maquinas.filter(m => m.aoVivo) }
    : linhaGeral

  return (
    <div className="p-4 flex flex-col gap-4">

      {/* Filtros */}
      <div className={`${cardPadded} flex items-end gap-3 flex-wrap`}>
        <div className="flex flex-col gap-1.5">
          <label className="text-xs text-texto-2">Linha</label>
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
          <label className="text-xs text-texto-2">Visão</label>
          <div className="flex">
            {OPCOES_VISAO.map(o => (
              <button
                key={o.valor}
                onClick={() => escolherVisao(o.valor)}
                className={`h-9 w-24 -ml-px first:ml-0 text-center text-xs font-medium border transition-colors ${
                  visao === o.valor
                    ? 'relative bg-accent-600 text-accent-fg border-accent-600'
                    : 'border-borda-forte text-texto-2 hover:bg-superficie-2'
                }`}
              >
                {o.rotulo}
              </button>
            ))}
          </div>
        </div>

        {dataFiltro === null && (
          <label className="flex items-center gap-2 h-9 cursor-pointer">
            <Switch checked={soEmMedicao} onChange={escolherSoEmMedicao} />
            <span className="text-xs text-texto-2">Só em medição</span>
          </label>
        )}

        {atualizadoEm && (
          <p className="ml-auto text-[10px] text-texto-3 self-center">
            Atualiza sozinho a cada 5 min · última às {atualizadoEm.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}
          </p>
        )}
      </div>

      {/* Grid de cards ou a visão geral da linha */}
      {loadingDados ? (
        <div className="flex items-center justify-center h-48 text-sm text-texto-3">Carregando...</div>
      ) : erro ? (
        <div className="flex items-center justify-center h-48 text-sm text-red-400">Erro: {erro}</div>
      ) : visao === 'linha' ? (
        linhaGeralVisivel && linhaGeralVisivel.maquinas.length > 0
          ? <LinhaGeral dados={linhaGeralVisivel} onAbrirMaquina={abrirDetalhe} />
          : <div className="flex items-center justify-center h-48 text-sm text-texto-3">{filtrarEmMedicao ? 'Nenhuma máquina em medição nesta linha' : 'Nenhuma máquina nesta linha'}</div>
      ) : dadosVisiveis.length === 0 ? (
        <div className="flex items-center justify-center h-48 text-sm text-texto-3">{filtrarEmMedicao ? 'Nenhuma máquina em medição nesta linha' : 'Nenhuma máquina nesta linha'}</div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {dadosVisiveis.map(m => (
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