// Tela de Medição — tabela das máquinas da linha do topo em medição agora (Manual ou IoT):
// máquina, modo, quem iniciou, data e hora do início, e o botão Finalizar (Administrador e
// Desenvolvedor). Manual pede a leitura final; IoT só confirma (a produção pendente é gravada
// pelo backend). Atualiza a cada 30 s.
import { useEffect, useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { linhaService } from '../../services/linhaService'
import { sessaoService } from '../../services/sessaoService'
import { coletaIotService } from '../../services/coletaIotService'
import { configuracaoService, type CampoMaquinaDto } from '../../services/configuracaoService'
import { mensagemErro } from '../../services/api'
import type { MaquinaLinha } from '../../types'
import LeituraFinalModal from '../../modals/LeituraFinalModal'
import ConfirmModal from '../../components/ConfirmModal'
import { cardBase, cardHeader } from '../../styles/cards'
import { table, tableHeadRow, tableHeadCell, tableBodyRow, tableBodyCell, tableBodyCellMuted } from '../../styles/tables'
import { btnDangerSm } from '../../styles/buttons'
import { badgeIot } from '../../styles/badges'
import { dataHora } from '../../utils/tempo'

interface EmAndamento {
  maquina: MaquinaLinha
  inicio: string | null
  usuarioNome: string | null
}

interface FinalizandoState {
  sessaoId: string
  maquinaNome: string
  medeProducao: boolean
  camposExtras: CampoMaquinaDto[]
}

interface Props {
  // Muda quando uma medição é iniciada nesta tela: recarrega a tabela
  versao: number
  onFinalizada?: () => void
}

export default function MedicoesEmAndamento({ versao, onFinalizada }: Props) {
  const { clienteId, linhaId, usuario } = useAuth()
  const podeFinalizar = usuario?.nivel === 'Administrador' || usuario?.nivel === 'Desenvolvedor'
  const [itens, setItens] = useState<EmAndamento[] | null>(null)
  const [recarregar, setRecarregar] = useState(0)
  const [erro, setErro] = useState<string | null>(null)

  const [finalizando, setFinalizando] = useState<FinalizandoState | null>(null)
  const [salvando, setSalvando] = useState(false)
  const [finalizandoColeta, setFinalizandoColeta] = useState<{ acompanhamentoId: string; maquinaNome: string } | null>(null)

  useEffect(() => {
    if (!clienteId || !linhaId) return
    let ativo = true
    async function carregar() {
      try {
        const linhas = await linhaService.getLinhasByCliente(clienteId!)
        const linha = linhas.find(l => l.id === linhaId)
        const ativas = (linha?.maquinas ?? []).filter(m => m.ativo && m.sessaoAtiva && m.sessaoAtivaId).sort((a, b) => a.ordem - b.ordem)
        // O início vem da própria sessão (a lista da linha não traz)
        const lista = await Promise.all(ativas.map(async m => {
          try {
            const s = await sessaoService.getById(m.sessaoAtivaId!)
            return { maquina: m, inicio: s.inicio, usuarioNome: s.usuarioNome ?? null }
          } catch {
            return { maquina: m, inicio: null, usuarioNome: null }
          }
        }))
        if (ativo) { setItens(lista); setErro(null) }
      } catch (e: unknown) {
        if (ativo) setErro(e instanceof Error ? e.message : 'Erro ao carregar as medições')
      }
    }
    carregar()
    const id = setInterval(carregar, 30000)
    return () => { ativo = false; clearInterval(id) }
  }, [clienteId, linhaId, versao, recarregar])

  async function pedirFinalizar(item: EmAndamento) {
    const m = item.maquina
    if (m.acompanhamentoId) {
      setFinalizandoColeta({ acompanhamentoId: m.acompanhamentoId, maquinaNome: m.maquinaNome })
      return
    }
    if (!m.sessaoAtivaId) return
    const campos = await configuracaoService.getCamposMaquina(m.maquinaId)
    setFinalizando({ sessaoId: m.sessaoAtivaId, maquinaNome: m.maquinaNome, medeProducao: m.medeProducao, camposExtras: campos.filter(c => c.ativo) })
  }

  async function confirmarFinalizar(producaoFinal: number, extras: { campoMaquinaId: string; valor: number }[]) {
    if (!finalizando) return
    setSalvando(true)
    setErro(null)
    try {
      await sessaoService.finalizar(finalizando.sessaoId, producaoFinal, 0, extras)
      setFinalizando(null)
      setRecarregar(r => r + 1)
      onFinalizada?.()
    } catch (e: unknown) {
      setErro(mensagemErro(e, 'Não foi possível finalizar a sessão.'))
    } finally {
      setSalvando(false)
    }
  }

  async function confirmarFinalizarColeta() {
    if (!finalizandoColeta) return
    const alvo = finalizandoColeta
    setFinalizandoColeta(null)
    setErro(null)
    try {
      await coletaIotService.finalizar(alvo.acompanhamentoId)
      setRecarregar(r => r + 1)
      onFinalizada?.()
    } catch (e: unknown) {
      setErro(mensagemErro(e, `Não foi possível finalizar a coleta da ${alvo.maquinaNome}.`))
    }
  }

  return (
    <div className={`${cardBase} w-full`}>
      <div className={cardHeader}>
        <span className="text-xs font-medium text-texto">Medições em andamento</span>
        <span className="text-[10px] text-texto-3">{itens ? `${itens.length} máquina${itens.length === 1 ? '' : 's'}` : ''}</span>
      </div>
      {erro && <p className="px-4 py-2 text-xs text-red-500">{erro}</p>}
      {itens === null ? (
        <p className="px-4 py-4 text-xs text-texto-3">Carregando...</p>
      ) : itens.length === 0 ? (
        <p className="px-4 py-4 text-xs text-texto-3">Nenhuma máquina em medição nesta linha</p>
      ) : (
        <table className={table}>
          <thead>
            <tr className={tableHeadRow}>
              <th className={tableHeadCell}>Máquina</th>
              <th className={tableHeadCell}>Modo</th>
              <th className={tableHeadCell}>Por</th>
              <th className={tableHeadCell}>Início</th>
              {podeFinalizar && <th className={tableHeadCell} />}
            </tr>
          </thead>
          <tbody>
            {itens.map(item => (
              <tr key={item.maquina.id} className={tableBodyRow}>
                <td className={tableBodyCell}>
                  {item.maquina.maquinaNome}
                  {item.maquina.acompanhamentoId && <span className={`ml-1.5 ${badgeIot}`}>IoT</span>}
                </td>
                <td className={tableBodyCellMuted}>{item.maquina.acompanhamentoId ? 'IoT' : 'Manual'}</td>
                <td className={tableBodyCellMuted}>{item.usuarioNome ?? '—'}</td>
                <td className={tableBodyCellMuted}>{dataHora(item.inicio)}</td>
                {podeFinalizar && (
                  <td className="py-2 pr-2 text-right">
                    <button onClick={() => pedirFinalizar(item)} className={btnDangerSm}>
                      {item.maquina.acompanhamentoId ? 'Finalizar coleta' : 'Finalizar medição'}
                    </button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <ConfirmModal
        open={finalizandoColeta !== null}
        titulo="Finalizar coleta"
        mensagem={finalizandoColeta
          ? `Finalizar a coleta IoT da ${finalizandoColeta.maquinaNome}? A produção ainda não gravada é gravada antes, e a máquina fica sem coleta até alguém iniciar de novo.`
          : ''}
        onConfirmar={confirmarFinalizarColeta}
        onCancelar={() => setFinalizandoColeta(null)}
      />
      <LeituraFinalModal
        open={finalizando !== null}
        camposExtras={finalizando?.camposExtras ?? []}
        medeProducao={finalizando?.medeProducao ?? true}
        salvando={salvando}
        onConfirmar={confirmarFinalizar}
        onCancelar={() => setFinalizando(null)}
      />
    </div>
  )
}
