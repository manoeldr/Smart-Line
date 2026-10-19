// Linha do tempo de uma máquina no relatório: tabela em ordem cronológica (horário, evento,
// motivo/tipo, duração), dividida em folhas de tamanho fixo pelo chamador (ver linhasPorFolha).
import type { EventoTimelineDto } from '../../services/sessaoDetalheService'


function hora(iso: string) {
  return new Date(iso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

function duracao(ms: number | null | undefined, emAndamento?: boolean) {
  if (ms == null) return ''
  const s = Math.max(0, Math.round(ms / 1000))
  const texto = s < 60 ? `${s} s`
    : s < 3600 ? `${Math.floor(s / 60)} min ${String(s % 60).padStart(2, '0')} s`
    : `${Math.floor(s / 3600)} h ${String(Math.floor((s % 3600) / 60)).padStart(2, '0')} min`
  return emAndamento ? `${texto} (até agora)` : texto
}

function descricao(e: EventoTimelineDto): { evento: string; detalhe: string; cor: string } {
  switch (e.tipo) {
    case 'Inicio': return { evento: 'Medição iniciada', detalhe: '', cor: 'text-texto' }
    case 'Fim': return { evento: e.motivoNome === 'ViradaDoDia' ? 'Dia encerrado à meia-noite' : 'Medição finalizada', detalhe: '', cor: 'text-texto' }
    case 'Marcha': return { evento: 'Voltou a produzir', detalhe: '', cor: 'text-green-700' }
    case 'SemComunicacao': return { evento: 'Sem comunicação', detalhe: 'fora do OEE', cor: 'text-texto-2' }
    default: {
      const tipo = e.motivoTipo === 'Planejada' ? 'programada' : e.motivoTipo === 'Externa' ? 'externa' : 'interna'
      return {
        evento: 'Parada',
        detalhe: e.motivoNome ? `${e.motivoNome} (${tipo})` : 'sem motivo (conta como interna)',
        cor: e.motivoTipo === 'Externa' ? 'text-[#d49a00]' : e.motivoTipo === 'Planejada' ? 'text-[#8b5cf6]' : 'text-[#e60000]',
      }
    }
  }
}

export default function LinhaDoTempo({ eventos, continuacao }: { eventos: EventoTimelineDto[]; continuacao: boolean }) {
  return (
    <table className="w-full text-[10pt] leading-tight border-collapse text-left table-fixed">
      <thead>
        <tr className="border-b border-texto text-[10pt] text-texto-3">
          <th className="text-left py-1 font-medium w-20">Horário</th>
          <th className="text-left py-1 font-medium w-40">Evento</th>
          <th className="text-left py-1 font-medium">Motivo</th>
          <th className="text-right py-1 font-medium w-28">Duração</th>
        </tr>
      </thead>
      <tbody>
        {continuacao && (
          <tr><td colSpan={4} className="py-0.5 italic text-texto-3">(continuação)</td></tr>
        )}
        {eventos.map((e, i) => {
          const d = descricao(e)
          return (
            <tr key={i} className="border-b border-borda-suave">
              {/* Uma linha por evento, sempre: motivo comprido é cortado com "…", para a conta de linhas por folha fechar */}
              <td className="py-0.5 text-texto-2 tabular-nums whitespace-nowrap">{hora(e.horario)}</td>
              <td className={`py-0.5 font-medium whitespace-nowrap ${d.cor}`}>{d.evento}</td>
              <td className="py-0.5 text-texto whitespace-nowrap overflow-hidden text-ellipsis" title={d.detalhe}>{d.detalhe}</td>
              <td className="py-0.5 text-right text-texto-2 tabular-nums whitespace-nowrap">{duracao(e.duracaoMs, e.emAndamento)}</td>
            </tr>
          )
        })}
      </tbody>
    </table>
  )
}
