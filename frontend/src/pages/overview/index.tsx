// Tela Overview — lista todas as linhas do cliente com status ao vivo das máquinas.
// Administrador/Desenvolvedor podem finalizar sessões ativas de outros usuários direto daqui.
// Clicar numa máquina abre o detalhe (o mesmo do Dashboard); na coleta Semi Automática ele
// mostra a coleta ao vivo. Finalizar uma coleta Semi Automática não pede leitura final: o
// contador é do WISE, e a produção pendente é gravada pelo próprio backend.
import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import { linhaService } from '../../services/linhaService'
import { sessaoService } from '../../services/sessaoService'
import { configuracaoService, type CampoMaquinaDto } from '../../services/configuracaoService'
import type { Linha } from '../../types'
import LinhaCard from './LinhaCard'
import LeituraFinalModal from '../../modals/LeituraFinalModal'
import MaquinaDetalheModal from '../../modals/MaquinaDetalheModal'
import ConfirmModal from '../../components/ConfirmModal'
import { coletaIotService } from '../../services/coletaIotService'
import { mensagemErro } from '../../services/api'
import { paradaColetaService } from '../../services/paradaColetaService'
import ParadasSemMotivoModal from '../../modals/ParadasSemMotivoModal'

interface OutletContext {
  dataFiltro: string | null
  setDataFiltro: (d: string | null) => void
  filtroOpen: boolean
  setFiltroOpen: (v: boolean) => void
}

interface FinalizandoState {
  sessaoId: string
  maquinaNome: string
  medeProducao: boolean
  camposExtras: CampoMaquinaDto[]
}

export default function Overview() {
  const { dataFiltro } = useOutletContext<OutletContext>()
  const { clienteId, usuario } = useAuth()
  const podeClassificar = ['Administrador', 'Desenvolvedor', 'Auditor'].includes(usuario?.nivel ?? '')
  const [linhas, setLinhas] = useState<Linha[]>([])
  // Cliente cujas linhas estão na tela: "Carregando..." só enquanto ele não bate com o
  // selecionado. A atualização a cada 30 s é silenciosa — trocar a tela inteira por
  // "Carregando..." desmontava o detalhe da máquina aberto, que piscava fechando e abrindo.
  const [clienteCarregado, setClienteCarregado] = useState<string | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  // Finalização de sessão de outro usuário (Admin/Desenvolvedor, via Overview)
  const [finalizando, setFinalizando] = useState<FinalizandoState | null>(null)
  const [salvandoFinalizacao, setSalvandoFinalizacao] = useState(false)

  // Coleta Semi Automática: finalização com confirmação simples
  const [finalizandoColeta, setFinalizandoColeta] = useState<{ acompanhamentoId: string; maquinaNome: string } | null>(null)
  const [erroAcao, setErroAcao] = useState<string | null>(null)

  // Detalhe da máquina clicada
  const [detalhe, setDetalhe] = useState<string | null>(null)
  const [recarregar, setRecarregar] = useState(0)

  // Paradas das coletas automáticas sem motivo nos últimos 7 dias (botão no topo)
  const [semMotivo, setSemMotivo] = useState(0)
  const [semMotivoAberto, setSemMotivoAberto] = useState(false)

  useEffect(() => {
    if (!clienteId) return
    let ativo = true
    async function carregar() {
      try {
        const data = await linhaService.getLinhasByCliente(clienteId!)
        if (!ativo) return
        setLinhas(data)
        setClienteCarregado(clienteId)
        setErro(null)
      } catch (e: unknown) {
        // Com as linhas já na tela, uma falha na atualização só mantém o que estava.
        if (ativo) setErro(e instanceof Error ? e.message : 'Erro ao carregar linhas')
      }
    }
    carregar()
    const id = setInterval(carregar, 30000)
    return () => { ativo = false; clearInterval(id) }
  }, [clienteId, recarregar])

  useEffect(() => {
    if (!clienteId) return
    let ativo = true
    async function contar() {
      const desde = new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString()
      try {
        const p = await paradaColetaService.pendentes({ clienteId: clienteId!, desde, limite: 1000 })
        if (ativo) setSemMotivo(p.length)
      } catch {
        // contagem é só um atalho: se falhar, o botão some
        if (ativo) setSemMotivo(0)
      }
    }
    contar()
    const id = setInterval(contar, 30000)
    return () => { ativo = false; clearInterval(id) }
  }, [clienteId, recarregar])

  async function handleFinalizarClick(maquinaLinhaId: string, maquinaNome: string, medeProducao: boolean) {
    // Acha a máquina pra pegar o sessaoAtivaId e o maquinaId (necessário pra buscar os campos extras)
    let sessaoId: string | null = null
    let maquinaId: string | null = null
    for (const linha of linhas) {
      const m = linha.maquinas.find(x => x.id === maquinaLinhaId)
      if (m) {
        // Coleta Semi Automática: confirmação simples, sem leitura final
        if (m.acompanhamentoId) {
          setFinalizandoColeta({ acompanhamentoId: m.acompanhamentoId, maquinaNome })
          return
        }
        sessaoId = m.sessaoAtivaId
        maquinaId = m.maquinaId
        break
      }
    }
    if (!sessaoId || !maquinaId) return

    const campos = await configuracaoService.getCamposMaquina(maquinaId)
    setFinalizando({
      sessaoId,
      maquinaNome,
      medeProducao,
      camposExtras: campos.filter(c => c.ativo),
    })
  }

  async function handleConfirmarFinalizar(producaoFinal: number, extras: { campoMaquinaId: string; valor: number }[]) {
    if (!finalizando) return
    setSalvandoFinalizacao(true)
    try {
      await sessaoService.finalizar(finalizando.sessaoId, producaoFinal, 0, extras)
      setFinalizando(null)
      const data = await linhaService.getLinhasByCliente(clienteId!)
      setLinhas(data)
    } catch {
      alert('Erro ao finalizar a sessão.')
    } finally {
      setSalvandoFinalizacao(false)
    }
  }

  async function handleConfirmarFinalizarColeta() {
    if (!finalizandoColeta) return
    const alvo = finalizandoColeta
    setFinalizandoColeta(null)
    setErroAcao(null)
    try {
      await coletaIotService.finalizar(alvo.acompanhamentoId)
      setRecarregar(r => r + 1)
    } catch (e) {
      setErroAcao(mensagemErro(e, `Não foi possível finalizar a coleta da ${alvo.maquinaNome}.`))
    }
  }

  if (!clienteId) {
    return (
      <div className="flex items-center justify-center h-48 text-sm text-zinc-400">
        Nenhum cliente selecionado
      </div>
    )
  }

  const carregado = clienteCarregado === clienteId

  if (!carregado && !erro) {
    return (
      <div className="flex items-center justify-center h-48 text-sm text-zinc-400">
        Carregando...
      </div>
    )
  }

  if (!carregado) {
    return (
      <div className="flex items-center justify-center h-48 text-sm text-red-400">
        Erro ao carregar linhas: {erro}
      </div>
    )
  }

  return (
    <div className="p-4 flex flex-col gap-3">
      {semMotivo > 0 && dataFiltro === null && (
        <div className="flex justify-end">
          <button
            onClick={() => setSemMotivoAberto(true)}
            className="h-8 px-3 border border-amber-300 dark:border-amber-800 text-xs text-amber-700 dark:text-amber-400 bg-amber-50 dark:bg-amber-950 hover:bg-amber-100 dark:hover:bg-amber-900 transition-colors"
          >
            Paradas sem motivo ({semMotivo})
          </button>
        </div>
      )}
      {erroAcao && (
        <div className="bg-red-50 dark:bg-red-950 border border-red-200 dark:border-red-800 px-3 py-2 text-xs text-red-600 dark:text-red-400 flex justify-between gap-3">
          <span>{erroAcao}</span>
          <button onClick={() => setErroAcao(null)} className="text-red-400 hover:text-red-600">fechar</button>
        </div>
      )}
      {linhas.length === 0 ? (
        <div className="flex items-center justify-center h-48 text-sm text-zinc-400">
          Nenhuma linha cadastrada
        </div>
      ) : (
        linhas.map(linha => (
          <LinhaCard
            key={linha.id}
            linha={linha}
            filtroAtivo={dataFiltro !== null}
            dataFiltro={dataFiltro}
            onFinalizarMaquina={handleFinalizarClick}
            onAbrirMaquina={setDetalhe}
          />
        ))
      )}

      <MaquinaDetalheModal
        open={detalhe !== null}
        maquinaLinhaId={detalhe}
        onFechar={() => setDetalhe(null)}
        onColetaFinalizada={() => setRecarregar(r => r + 1)}
      />

      <ParadasSemMotivoModal
        open={semMotivoAberto}
        clienteId={clienteId}
        linhas={linhas}
        podeClassificar={podeClassificar}
        onFechar={() => setSemMotivoAberto(false)}
        onAlterado={() => setRecarregar(r => r + 1)}
      />

      <ConfirmModal
        open={finalizandoColeta !== null}
        titulo="Finalizar coleta"
        mensagem={finalizandoColeta
          ? `Finalizar a coleta Semi Automática da ${finalizandoColeta.maquinaNome}? A produção ainda não gravada é gravada antes, e a máquina fica sem coleta até alguém iniciar de novo.`
          : ''}
        onConfirmar={handleConfirmarFinalizarColeta}
        onCancelar={() => setFinalizandoColeta(null)}
      />

      <LeituraFinalModal
        open={finalizando !== null}
        camposExtras={finalizando?.camposExtras ?? []}
        medeProducao={finalizando?.medeProducao ?? true}
        salvando={salvandoFinalizacao}
        onConfirmar={handleConfirmarFinalizar}
        onCancelar={() => setFinalizando(null)}
      />
    </div>
  )
}