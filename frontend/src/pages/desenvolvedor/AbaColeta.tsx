// Aba "Coleta" da Área do Desenvolvedor: o broker (antes em Dispositivos IoT) e cada coleta
// Semi Automática em andamento com o que o motor vê agora — WISE, situação, última mensagem,
// parada aberta, sensores e os contadores brutos. Atualiza a cada 2 s.
import { useEffect, useState } from 'react'
import { coletaIotService, type ColetaIotPainelDto } from '../../services/coletaIotService'
import { dispositivoIotService, type StatusColetaIotDto } from '../../services/dispositivoIotService'
import { mensagemErro } from '../../services/api'
import StatusBroker from '../../components/iot/StatusBroker'
import SituacaoColetaTexto from '../../components/iot/SituacaoColetaTexto'
import { tempoDesde, hora } from '../../utils/tempo'
import { cardPadded } from '../../styles/cards'
import { table, tableHeadRow, tableHeadCell, tableBodyRow, tableBodyCell, tableBodyCellMuted } from '../../styles/tables'

const INTERVALO_MS = 2000

function duracaoDesde(iso: string, agora: Date): string {
  const s = Math.max(0, Math.round((agora.getTime() - new Date(iso).getTime()) / 1000))
  if (s < 60) return `${s} s`
  if (s < 3600) return `${Math.floor(s / 60)} min ${String(s % 60).padStart(2, '0')} s`
  return `${Math.floor(s / 3600)} h ${String(Math.floor((s % 3600) / 60)).padStart(2, '0')} min`
}

export default function AbaColeta() {
  const [status, setStatus] = useState<StatusColetaIotDto | null>(null)
  const [coletas, setColetas] = useState<ColetaIotPainelDto[]>([])
  const [carregado, setCarregado] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [agora, setAgora] = useState(new Date())

  useEffect(() => {
    let ativo = true
    async function carregar() {
      try {
        const [s, c] = await Promise.all([dispositivoIotService.status(), coletaIotService.painel()])
        if (!ativo) return
        setStatus(s)
        setColetas(c)
        setErro(null)
      } catch (e) {
        if (ativo) setErro(mensagemErro(e, 'Não foi possível consultar a coleta.'))
      } finally {
        if (ativo) { setCarregado(true); setAgora(new Date()) }
      }
    }
    carregar()
    const id = setInterval(carregar, INTERVALO_MS)
    return () => { ativo = false; clearInterval(id) }
  }, [])

  return (
    <div className="flex flex-col gap-4">
      {erro && <p className="text-xs text-red-500">{erro}</p>}
      {status && <StatusBroker status={status} />}

      <div className={cardPadded}>
        <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100 mb-3">
          Coletas em andamento {carregado && <span className="text-zinc-400 font-normal">({coletas.length})</span>}
        </p>
        {!carregado && <p className="text-xs text-zinc-400">Carregando...</p>}
        {carregado && coletas.length === 0 && <p className="text-xs text-zinc-400">Nenhuma máquina em medição Semi Automática.</p>}
        {coletas.length > 0 && (
          <table className={table}>
            <thead>
              <tr className={tableHeadRow}>
                <th className={tableHeadCell}>Máquina</th>
                <th className={tableHeadCell}>WISE</th>
                <th className={tableHeadCell}>Situação</th>
                <th className={tableHeadCell}>Última mensagem</th>
                <th className={tableHeadCell}>Sessão</th>
                <th className={tableHeadCell}>Sensores</th>
                <th className={tableHeadCell}>Contadores (brutos)</th>
              </tr>
            </thead>
            <tbody>
              {coletas.map(({ coleta, situacao, ultimaMensagem, producaoSessao, refugoSessao, sensores, contadores, wiseConectado }) => (
                <tr key={coleta.acompanhamentoId} className={tableBodyRow}>
                  <td className={tableBodyCell}>
                    {coleta.maquina}
                    <span className="block text-[10px] text-zinc-400">{coleta.cliente} · {coleta.linha}</span>
                    <span className="block text-[10px] text-zinc-400">
                      desde {hora(coleta.iniciadoEm)} por {coleta.usuario} · parada após {coleta.tempoDeteccaoParadaSegundos} s
                    </span>
                  </td>
                  <td className={tableBodyCell}>
                    <span className="font-mono">{coleta.enderecoIp ?? '—'}</span>
                    <span className={`block text-[10px] ${wiseConectado ? 'text-emerald-600 dark:text-emerald-400' : 'text-zinc-400'}`}>
                      {wiseConectado ? 'conectado ao broker' : 'sem conexão'}
                    </span>
                    <span className="block text-[10px] text-zinc-400">
                      {coleta.canais.map(c => c.multiplicador > 1 ? `${c.canal}×${c.multiplicador}` : c.canal).join(' ')}
                    </span>
                  </td>
                  <td className={tableBodyCell}>
                    <SituacaoColetaTexto situacao={situacao} />
                    {coleta.paradaAberta && (
                      <span className="block text-[10px] text-zinc-400">
                        {coleta.paradaAberta.motivo ?? 'sem classificação'} · há {duracaoDesde(coleta.paradaAberta.inicio, agora)}
                      </span>
                    )}
                    {coleta.semComunicacaoDesde && (
                      <span className="block text-[10px] text-zinc-400">sem comunicação há {duracaoDesde(coleta.semComunicacaoDesde, agora)}</span>
                    )}
                    {coleta.paradasNaoClassificadas > 0 && (
                      <span className="block text-[10px] text-amber-600 dark:text-amber-400">
                        {coleta.paradasNaoClassificadas} {coleta.paradasNaoClassificadas === 1 ? 'parada' : 'paradas'} sem classificação
                      </span>
                    )}
                  </td>
                  <td className={tableBodyCellMuted}>
                    {ultimaMensagem ? tempoDesde(ultimaMensagem, agora) : 'nenhuma'}
                    {ultimaMensagem && <span className="block text-[10px]">{hora(ultimaMensagem)}</span>}
                  </td>
                  <td className={tableBodyCellMuted}>
                    {producaoSessao.toLocaleString('pt-BR')} garrafas
                    <span className="block text-[10px]">refugo {refugoSessao.toLocaleString('pt-BR')}</span>
                    {coleta.ultimaConsolidacao && <span className="block text-[10px]">gravado às {hora(coleta.ultimaConsolidacao)}</span>}
                  </td>
                  <td className={tableBodyCellMuted}>
                    {sensores.length === 0 && '—'}
                    {sensores.map(s => (
                      <span key={s.canal} className={`block text-[10px] ${s.emAlarme ? 'text-red-600 dark:text-red-400' : ''}`} title={s.texto}>
                        {s.canal}={s.valorBruto ? 1 : 0} {s.nome}{s.emAlarme ? ' (alarme)' : ''}
                      </span>
                    ))}
                  </td>
                  <td className={tableBodyCellMuted}>
                    <span className="font-mono">
                      {Object.entries(contadores).length === 0
                        ? '—'
                        : Object.entries(contadores).map(([canal, valor]) => `${canal}=${valor}`).join(' ')}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  )
}
