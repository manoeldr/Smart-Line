// Paradas sem motivo das coletas automáticas do cliente: lista com filtros (linha, máquina,
// período) e o botão Classificar em cada uma. Aberto pelo botão "Paradas sem classificação" do Overview.
import { useEffect, useState } from 'react'
import type { Linha } from '../types'
import { paradaColetaService, type ParadaColetaDto } from '../services/paradaColetaService'
import { mensagemErro } from '../services/api'
import EditarMotivoParadaModal from './EditarMotivoParadaModal'
import HistoricoParadaModal from './HistoricoParadaModal'
import { dataHora } from '../utils/tempo'
import { btnPrimaryXs, btnSecondarySm } from '../styles/buttons'
import { inputBase, label } from '../styles/inputs'
import { modalOverlay, modalPanel, modalHeader, modalTitle, modalSubtitle, modalBody, modalFooter } from '../styles/modals'
import { table, tableHeadRow, tableHeadCell, tableBodyRow, tableBodyCell, tableBodyCellMuted, tableActionsCell } from '../styles/tables'

interface Props {
  open: boolean
  clienteId: string
  linhas: Linha[]
  podeClassificar: boolean
  onFechar: () => void
  // Alguma parada foi classificada (o Overview atualiza a contagem)
  onAlterado: () => void
}

function diasAtras(dias: number) {
  const d = new Date()
  d.setDate(d.getDate() - dias)
  return d.toISOString().slice(0, 10)
}

function duracao(segundos: number) {
  const h = Math.floor(segundos / 3600)
  const m = Math.floor((segundos % 3600) / 60)
  return h > 0 ? `${h}h ${m}min` : m > 0 ? `${m} min` : `${Math.round(segundos)} s`
}

export default function ParadasSemMotivoModal(props: Props) {
  return props.open ? <Conteudo {...props} /> : null
}

function Conteudo({ clienteId, linhas, podeClassificar, onFechar, onAlterado }: Props) {
  const [linhaId, setLinhaId] = useState('')
  const [maquinaLinhaId, setMaquinaLinhaId] = useState('')
  const [desde, setDesde] = useState(diasAtras(7))
  const [ate, setAte] = useState(diasAtras(0))

  const [paradas, setParadas] = useState<ParadaColetaDto[] | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [recarregar, setRecarregar] = useState(0)

  const [editando, setEditando] = useState<ParadaColetaDto | null>(null)
  const [historico, setHistorico] = useState<string | null>(null)

  useEffect(() => {
    let ativo = true
    // Datas do filtro são dias locais: do começo de "desde" ao fim de "até".
    const inicio = desde ? new Date(`${desde}T00:00:00`).toISOString() : undefined
    const fim = ate ? new Date(`${ate}T23:59:59`).toISOString() : undefined
    paradaColetaService.pendentes({
      clienteId,
      linhaId: linhaId || undefined,
      maquinaLinhaId: maquinaLinhaId || undefined,
      desde: inicio,
      ate: fim,
      limite: 500,
    })
      .then(p => { if (ativo) { setParadas(p); setErro(null) } })
      .catch(e => { if (ativo) setErro(mensagemErro(e, 'Erro ao carregar as paradas.')) })
    return () => { ativo = false }
  }, [clienteId, linhaId, maquinaLinhaId, desde, ate, recarregar])

  const maquinasDaLinha = linhas.find(l => l.id === linhaId)?.maquinas ?? []

  function aoClassificar() {
    setEditando(null)
    setRecarregar(r => r + 1)
    onAlterado()
  }

  return (
    <div className={modalOverlay}>
      <div className={`${modalPanel} w-[820px] max-h-[90vh]`}>
        <div className={modalHeader}>
          <p className={modalTitle}>Paradas sem classificação</p>
          <p className={modalSubtitle}>Paradas das coletas automáticas que os sensores não explicaram. Enquanto ninguém classificar, contam como internas.</p>
        </div>

        <div className={modalBody}>
          {/* Filtros */}
          <div className="grid grid-cols-4 gap-3">
            <div>
              <label className={label}>Linha</label>
              <select value={linhaId} onChange={e => { setLinhaId(e.target.value); setMaquinaLinhaId('') }} className={inputBase}>
                <option value="">Todas</option>
                {linhas.map(l => <option key={l.id} value={l.id}>{l.nome}</option>)}
              </select>
            </div>
            <div>
              <label className={label}>Máquina</label>
              <select value={maquinaLinhaId} onChange={e => setMaquinaLinhaId(e.target.value)} disabled={!linhaId} className={inputBase}>
                <option value="">Todas</option>
                {[...maquinasDaLinha].sort((a, b) => a.ordem - b.ordem).map(m => <option key={m.id} value={m.id}>{m.maquinaNome}</option>)}
              </select>
            </div>
            <div>
              <label className={label}>De</label>
              <input type="date" value={desde} onChange={e => setDesde(e.target.value)} className={inputBase} />
            </div>
            <div>
              <label className={label}>Até</label>
              <input type="date" value={ate} onChange={e => setAte(e.target.value)} className={inputBase} />
            </div>
          </div>

          {erro && <p className="text-xs text-red-600 dark:text-red-400">{erro}</p>}

          {!paradas ? (
            !erro && <p className="text-xs text-zinc-400">Carregando...</p>
          ) : paradas.length === 0 ? (
            <p className="text-xs text-zinc-400">Nenhuma parada sem classificação neste período.</p>
          ) : (
            <table className={table}>
              <thead>
                <tr className={tableHeadRow}>
                  <th className={tableHeadCell}>Máquina</th>
                  <th className={tableHeadCell}>Início</th>
                  <th className={tableHeadCell}>Duração</th>
                  <th className="py-2" />
                </tr>
              </thead>
              <tbody>
                {paradas.map(p => (
                  <tr key={p.id} className={tableBodyRow}>
                    <td className={tableBodyCell}>
                      {p.maquina}
                      <span className="block text-[10px] text-zinc-400">{p.linha}</span>
                    </td>
                    <td className={tableBodyCellMuted}>{dataHora(p.inicio)}</td>
                    <td className={tableBodyCellMuted}>
                      {duracao(p.duracaoSegundos)}{p.fim ? '' : ' (em andamento)'}
                    </td>
                    <td className={tableActionsCell}>
                      <button onClick={() => setHistorico(p.id)} className="text-[10px] text-zinc-400 hover:text-zinc-700 dark:hover:text-zinc-200 hover:underline">
                        Histórico
                      </button>
                      {podeClassificar && (
                        <button onClick={() => setEditando(p)} className={btnPrimaryXs}>Classificar</button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        <div className={modalFooter}>
          <button onClick={onFechar} className={btnSecondarySm}>Fechar</button>
        </div>
      </div>

      <EditarMotivoParadaModal
        alvo={editando ? { paradaId: editando.id, maquinaId: editando.maquinaId } : null}
        onFechar={() => setEditando(null)}
        onSalvo={aoClassificar}
      />
      <HistoricoParadaModal paradaId={historico} onFechar={() => setHistorico(null)} />
    </div>
  )
}
