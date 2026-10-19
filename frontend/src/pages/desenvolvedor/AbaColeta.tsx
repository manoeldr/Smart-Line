// Aba "Coleta" da Área do Desenvolvedor: o broker (antes em Dispositivos IoT) e cada coleta
// Semi Automática em andamento com o que o motor vê agora — WISE, situação, última mensagem,
// parada aberta, sensores e os contadores brutos. Atualiza a cada 2 s.
import { useEffect, useState } from 'react'
import { coletaIotService, type ColetaIotPainelDto } from '../../services/coletaIotService'
import { dispositivoIotService, type StatusColetaIotDto } from '../../services/dispositivoIotService'
import { desenvolvedorService, type WiseVistoDto } from '../../services/desenvolvedorService'
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
  const [vistos, setVistos] = useState<WiseVistoDto[]>([])
  const [carregado, setCarregado] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [agora, setAgora] = useState(new Date())

  useEffect(() => {
    let ativo = true
    async function carregar() {
      try {
        const [s, c, v] = await Promise.all([dispositivoIotService.status(), coletaIotService.painel(), desenvolvedorService.wisesVistos()])
        if (!ativo) return
        setStatus(s)
        setColetas(c)
        setVistos(v)
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
        <p className="text-xs font-medium text-texto mb-3">
          Coletas em andamento {carregado && <span className="text-texto-3 font-normal">({coletas.length})</span>}
        </p>
        {!carregado && <p className="text-xs text-texto-3">Carregando...</p>}
        {carregado && coletas.length === 0 && <p className="text-xs text-texto-3">Nenhuma máquina em medição Semi Automática.</p>}
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
                    <span className="block text-[10px] text-texto-3">{coleta.cliente} · {coleta.linha}</span>
                    <span className="block text-[10px] text-texto-3">
                      desde {hora(coleta.iniciadoEm)} por {coleta.usuario} · parada após {coleta.tempoDeteccaoParadaSegundos} s
                    </span>
                  </td>
                  <td className={tableBodyCell}>
                    <span className="font-mono">{coleta.enderecoIp ?? '—'}</span>
                    <span className={`block text-[10px] ${wiseConectado ? 'text-emerald-600 dark:text-emerald-400' : 'text-texto-3'}`}>
                      {wiseConectado ? 'conectado ao broker' : 'sem conexão'}
                    </span>
                    <span className="block text-[10px] text-texto-3">
                      {coleta.canais.map(c => c.multiplicador > 1 ? `${c.canal}×${c.multiplicador}` : c.canal).join(' ')}
                    </span>
                  </td>
                  <td className={tableBodyCell}>
                    <SituacaoColetaTexto situacao={situacao} />
                    {coleta.paradaAberta && (
                      <span className="block text-[10px] text-texto-3">
                        {coleta.paradaAberta.motivo ?? 'sem classificação'} · há {duracaoDesde(coleta.paradaAberta.inicio, agora)}
                      </span>
                    )}
                    {coleta.semComunicacaoDesde && (
                      <span className="block text-[10px] text-texto-3">sem comunicação há {duracaoDesde(coleta.semComunicacaoDesde, agora)}</span>
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
                        {s.canal}={s.valorBruto ? 1 : 0} {s.nome}{s.emAlarme ? ' (falha pela regra)' : ''}
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

      {/* Todo IP que publicou no broker desde que o backend subiu: se está aqui, rede e MQTT estão certos */}
      <div className={cardPadded}>
        <p className="text-xs font-medium text-texto mb-3">
          WISE vistos pelo broker desde que o backend subiu {carregado && <span className="text-texto-3 font-normal">({vistos.length})</span>}
        </p>
        {carregado && vistos.length === 0 && <p className="text-xs text-texto-3">Nenhuma mensagem recebida ainda.</p>}
        {vistos.length > 0 && (
          <table className={table}>
            <thead>
              <tr className={tableHeadRow}>
                <th className={tableHeadCell}>IP</th>
                <th className={tableHeadCell}>Cadastro</th>
                <th className={tableHeadCell}>Conexão</th>
                <th className={tableHeadCell}>Identificação</th>
                <th className={tableHeadCell}>Último tópico</th>
                <th className={tableHeadCell}>Mensagens</th>
                <th className={tableHeadCell}>Última mensagem</th>
              </tr>
            </thead>
            <tbody>
              {vistos.map(w => (
                <tr key={w.enderecoIp} className={tableBodyRow}>
                  <td className={`${tableBodyCell} font-mono`}>{w.enderecoIp}</td>
                  <td className={tableBodyCellMuted}>
                    {w.cadastrado ? (w.nome ?? 'cadastrado') : <span className="text-amber-600 dark:text-amber-400">sem cadastro</span>}
                  </td>
                  <td className={`${tableBodyCellMuted} ${w.conectadoAgora ? 'text-emerald-600 dark:text-emerald-400' : ''}`}>
                    {w.conectadoAgora ? 'conectado' : 'desconectado'}
                  </td>
                  <td className={`${tableBodyCellMuted} font-mono`}>{w.clientId || '—'}</td>
                  <td className={`${tableBodyCellMuted} font-mono`}>{w.topico}</td>
                  <td className={tableBodyCellMuted}>{w.mensagens.toLocaleString('pt-BR')}</td>
                  <td className={tableBodyCellMuted}>
                    {tempoDesde(w.ultimaMensagemUtc, agora)}
                    <span className="block text-[10px]">{hora(w.ultimaMensagemUtc)}</span>
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
