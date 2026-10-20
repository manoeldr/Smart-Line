// Tela Overview — a linha escolhida no topo (cliente · linha) com status ao vivo das máquinas.
// O Finalizar das medições fica na tela de Medição; aqui cada card abre as paradas sem classificação.
// Clicar numa máquina abre o detalhe (o mesmo do Dashboard); na coleta IoT ele
// mostra a coleta ao vivo. Finalizar uma coleta IoT não pede leitura final: o
// contador é do WISE, e a produção pendente é gravada pelo próprio backend.
import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import { linhaService } from '../../services/linhaService'
import type { Linha } from '../../types'
import LinhaCard from './LinhaCard'
import GraficoPrincipalMotivo from './GraficoPrincipalMotivo'
import TimelineMaquinas from './TimelineMaquinas'
import GaugesCritica from './GaugesCritica'
import { useLinhaGeral } from './useLinhaGeral'
import MaquinaDetalheModal from '../../modals/MaquinaDetalheModal'
import ParadasSemMotivoModal from '../../modals/ParadasSemMotivoModal'
import { useParametroUrl } from '../../hooks/useParametroUrl'

interface OutletContext {
  dataFiltro: string | null
  setDataFiltro: (d: string | null) => void
  filtroOpen: boolean
  setFiltroOpen: (v: boolean) => void
}


export default function Overview() {
  const { dataFiltro } = useOutletContext<OutletContext>()
  const { clienteId, usuario, linhaId } = useAuth()
  const podeClassificar = ['Administrador', 'Desenvolvedor', 'Auditor'].includes(usuario?.nivel ?? '')
  const [linhas, setLinhas] = useState<Linha[]>([])
  // Cliente cujas linhas estão na tela: "Carregando..." só enquanto ele não bate com o
  // selecionado. A atualização a cada 30 s é silenciosa — trocar a tela inteira por
  // "Carregando..." desmontava o detalhe da máquina aberto, que piscava fechando e abrindo.
  const [clienteCarregado, setClienteCarregado] = useState<string | null>(null)
  const [erro, setErro] = useState<string | null>(null)


  // Detalhe da máquina clicada
  // Modais abertos ficam na URL (?maquina=, ?semMotivo=1) para o F5 reabri-los.
  const [detalhe, setDetalhe] = useParametroUrl('maquina')
  // Com o filtro por data, o detalhe abre a sessão daquele dia (não a última da máquina)
  const [sessaoDetalhe, setSessaoDetalhe] = useParametroUrl('sessao')
  const [recarregar, setRecarregar] = useState(0)
  // Versão da atualização de 30 s: o gráfico de motivos recarrega junto
  const [versaoAtualizacao, setVersaoAtualizacao] = useState(0)

  // Classificação das paradas sem motivo, aberta pelo card da máquina (?semMotivo=<máquina> para o F5)
  const [semMotivoUrl, setSemMotivoUrl] = useParametroUrl('semMotivo')
  const semMotivoAberto = semMotivoUrl !== null

  useEffect(() => {
    if (!clienteId) return
    let ativo = true
    async function carregar() {
      try {
        const data = await linhaService.getLinhasByCliente(clienteId!, dataFiltro)
        if (!ativo) return
        setLinhas(data)
        setClienteCarregado(clienteId)
        setErro(null)
        setVersaoAtualizacao(v => v + 1)
      } catch (e: unknown) {
        // Com as linhas já na tela, uma falha na atualização só mantém o que estava.
        if (ativo) setErro(e instanceof Error ? e.message : 'Erro ao carregar linhas')
      }
    }
    carregar()
    const id = setInterval(carregar, 30000)
    return () => { ativo = false; clearInterval(id) }
  }, [clienteId, recarregar, dataFiltro])


  // Só a linha escolhida no topo (sem escolha válida ainda: a primeira)
  const linhasNaTela = linhas.filter(l => l.id === linhaId)
  const linhasVisiveis = linhasNaTela.length > 0 ? linhasNaTela : linhas.slice(0, 1)
  const colunas = Math.max(1, ...linhasVisiveis.map(l => l.maquinas.length))
  // Visão geral da linha na tela (motivos e indicadores da crítica), atualizada com os cards
  const linhaGeral = useLinhaGeral(linhasVisiveis[0]?.id ?? '', dataFiltro, versaoAtualizacao)

  if (!clienteId) {
    return (
      <div className="flex items-center justify-center h-48 text-sm text-texto-3">
        Nenhum cliente selecionado
      </div>
    )
  }

  const carregado = clienteCarregado === clienteId


  if (!carregado && !erro) {
    return (
      <div className="flex items-center justify-center h-48 text-sm text-texto-3">
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
      {linhasVisiveis[0] && <GaugesCritica dados={linhaGeral} />}
      {linhas.length === 0 ? (
        <div className="flex items-center justify-center h-48 text-sm text-texto-3">
          Nenhuma linha cadastrada
        </div>
      ) : (
        linhasVisiveis.map(linha => (
          <LinhaCard
            key={linha.id}
            linha={linha}
            filtroAtivo={dataFiltro !== null}
            dataFiltro={dataFiltro}
            onParadasSemMotivo={id => setSemMotivoUrl(id)}
            onAbrirMaquina={(maquinaLinhaId, sessaoId) => { setSessaoDetalhe(sessaoId ?? null); setDetalhe(maquinaLinhaId) }}
            colunas={colunas}
          />
        ))
      )}
      {/* Abaixo da linha: gráficos lado a lado, o de motivos na metade esquerda */}
      {linhasVisiveis[0] && (
        <>
          <div className="grid grid-cols-2 gap-3">
            <GraficoPrincipalMotivo dados={linhaGeral} />
            <TimelineMaquinas linha={linhasVisiveis[0]} versao={versaoAtualizacao} />
          </div>
        </>
      )}

      <MaquinaDetalheModal
        open={detalhe !== null}
        maquinaLinhaId={detalhe}
        sessaoId={sessaoDetalhe}
        onFechar={() => { setDetalhe(null); setSessaoDetalhe(null) }}
        onColetaFinalizada={() => setRecarregar(r => r + 1)}
      />

      <ParadasSemMotivoModal
        open={semMotivoAberto}
        clienteId={clienteId}
        linhas={linhas}
        podeClassificar={podeClassificar}
        onFechar={() => setSemMotivoUrl(null)}
        onAlterado={() => setRecarregar(r => r + 1)}
        maquinaLinhaIdInicial={semMotivoUrl}
      />

    </div>
  )
}