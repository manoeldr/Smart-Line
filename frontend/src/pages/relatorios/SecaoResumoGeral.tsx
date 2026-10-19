// Seção 2 do relatório — Resumo Geral da máquina crítica, em tempo bruto (como a seção 1):
// medido = fim − início; disponível = medido − paradas programadas (pausas da medição);
// produzindo = disponível − paradas internas. As externas aparecem em tempo e eventos, mas
// não descontam do tempo produzindo (fora da responsabilidade da máquina, como no OEE).
import type { SessaoDetalheDto } from '../../services/sessaoDetalheService'
import { duracaoHoras, inteiro } from './formatos'

function Quadro({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div className="min-w-0 border border-borda bg-superficie-2 px-2 py-1.5">
      <p className="text-[10pt] text-texto-3 leading-tight whitespace-nowrap overflow-hidden">{rotulo}</p>
      <p className="text-[10pt] leading-snug text-texto font-medium">{valor}</p>
    </div>
  )
}

// agora: referência do fim de uma medição em andamento
export default function SecaoResumoGeral({ sessao, agora }: { sessao: SessaoDetalheDto; agora: Date }) {
  // Backend antigo (sem os campos do OEE no detalhe) manda vazio: vale 0
  const planejadoMs = sessao.tempoPlanejadoMs ?? 0
  const internoMs = sessao.tempoInternoMs ?? 0
  const externoMs = sessao.tempoExternoMs ?? 0
  const eventosInternas = sessao.numParadasInternas ?? 0
  const eventosExternas = sessao.numParadasExternas ?? 0

  const fimMs = sessao.fim ? new Date(sessao.fim).getTime() : agora.getTime()
  const medidoMs = Math.max(0, fimMs - new Date(sessao.inicio).getTime())
  const disponivelMs = Math.max(0, medidoMs - planejadoMs)
  const produzindoMs = Math.max(0, disponivelMs - internoMs)

  return (
    <section className="break-inside-avoid mt-5">
      <h2 className="text-[14pt] font-semibold uppercase tracking-wider text-texto-2 border-b-2 border-texto pb-1 mb-3">
        Resumo geral
      </h2>
      <div className="flex flex-col gap-2">
        <div className="grid grid-cols-3 gap-2">
          <Quadro rotulo="Tempo medido" valor={duracaoHoras(medidoMs)} />
          <Quadro rotulo="Tempo disponível" valor={duracaoHoras(disponivelMs)} />
          <Quadro rotulo="Tempo produzindo" valor={duracaoHoras(produzindoMs)} />
        </div>
        <div className="grid grid-cols-3 gap-2">
          <Quadro rotulo="Produção total" valor={`${inteiro(sessao.producao)} garrafas`} />
          <Quadro rotulo="Paradas programadas" valor={duracaoHoras(planejadoMs)} />
          <Quadro rotulo="Paradas internas (tempo)" valor={duracaoHoras(internoMs)} />
        </div>
        <div className="grid grid-cols-3 gap-2">
          <Quadro rotulo="Paradas internas" valor={String(eventosInternas)} />
          <Quadro rotulo="Paradas externas (tempo)" valor={duracaoHoras(externoMs)} />
          <Quadro rotulo="Paradas externas" valor={String(eventosExternas)} />
        </div>
      </div>
    </section>
  )
}
