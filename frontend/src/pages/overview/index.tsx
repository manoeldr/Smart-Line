// Tela Overview — lista todas as linhas do cliente com status ao vivo das máquinas.
// Administrador/Desenvolvedor podem finalizar sessões ativas de outros usuários direto daqui.
import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import { linhaService } from '../../services/linhaService'
import { sessaoService } from '../../services/sessaoService'
import { configuracaoService, type CampoMaquinaDto } from '../../services/configuracaoService'
import type { Linha } from '../../types'
import LinhaCard from './LinhaCard'
import LeituraFinalModal from '../../modals/LeituraFinalModal'

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
  const { clienteId } = useAuth()
  const [linhas, setLinhas] = useState<Linha[]>([])
  const [loading, setLoading] = useState(true)
  const [erro, setErro] = useState<string | null>(null)

  // Finalização de sessão de outro usuário (Admin/Desenvolvedor, via Overview)
  const [finalizando, setFinalizando] = useState<FinalizandoState | null>(null)
  const [salvandoFinalizacao, setSalvandoFinalizacao] = useState(false)

  useEffect(() => {
    if (!clienteId) return
    async function carregar() {
      setLoading(true)
      setErro(null)
      try {
        const data = await linhaService.getLinhasByCliente(clienteId!)
        setLinhas(data)
      } catch (e: unknown) {
        setErro(e instanceof Error ? e.message : 'Erro ao carregar linhas')
      } finally {
        setLoading(false)
      }
    }
    carregar()
    const id = setInterval(carregar, 30000)
    return () => clearInterval(id)
  }, [clienteId])

  async function handleFinalizarClick(maquinaLinhaId: string, maquinaNome: string, medeProducao: boolean) {
    // Acha a máquina pra pegar o sessaoAtivaId e o maquinaId (necessário pra buscar os campos extras)
    let sessaoId: string | null = null
    let maquinaId: string | null = null
    for (const linha of linhas) {
      const m = linha.maquinas.find(x => x.id === maquinaLinhaId)
      if (m) {
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

  if (!clienteId) {
    return (
      <div className="flex items-center justify-center h-48 text-sm text-zinc-400">
        Nenhum cliente selecionado
      </div>
    )
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center h-48 text-sm text-zinc-400">
        Carregando...
      </div>
    )
  }

  if (erro) {
    return (
      <div className="flex items-center justify-center h-48 text-sm text-red-400">
        Erro ao carregar linhas: {erro}
      </div>
    )
  }

  return (
    <div className="p-4 flex flex-col gap-3">
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
          />
        ))
      )}

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