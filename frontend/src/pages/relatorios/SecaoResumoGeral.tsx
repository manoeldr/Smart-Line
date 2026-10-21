// Seção 2 do relatório — Resumo Geral da máquina crítica, com os MESMOS números do OEE (vêm do
// backend, nada é recalculado aqui): tempo medido = fim − início = rodando + parada interna +
// parada externa + programadas + sem comunicação. Disponível = medido − sem comunicação −
// programadas. Sem comunicação fica fora do OEE, mas aparece como fatia.
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

// agora: referência do fim de uma medição em andamento (o backend já calculou "até agora")
export default function SecaoResumoGeral({ sessao }: { sessao: SessaoDetalheDto; agora: Date }) {
  // Backend antigo (sem os campos do OEE no detalhe) manda vazio: vale 0
  const medidoMs = sessao.tempoTotalMs ?? 0
  const disponivelMs = sessao.tempoDisponivelMs ?? 0
  const rodandoMs = sessao.tempoRodandoMs
  // Produção: a do OEE (com comunicação) + a feita sem comunicação (fora do OEE) = total
  const producaoSemComunicacao = sessao.pontosProducao.reduce((soma, p) => soma + (p.semComunicacao ?? 0), 0)
  const producaoTotal = sessao.producao + producaoSemComunicacao
  const internoMs = sessao.tempoInternoMs ?? 0
  const externoMs = sessao.tempoExternoMs ?? 0
  const semComunicacaoMs = sessao.tempoSemComunicacaoMs ?? 0
  const eventosInternas = sessao.numParadasInternas ?? 0
  const eventosExternas = sessao.numParadasExternas ?? 0

  return (
    <section className="break-inside-avoid mt-5">
      <h2 className="text-[14pt] font-semibold uppercase tracking-wider text-texto-2 border-b-2 border-texto pb-1 mb-3">
        Resumo geral
      </h2>
      {/* Sem comunicação (tempo e produção, só na medição IoT) só quando houve; paradas programadas não entram */}
      <div className="grid grid-cols-3 gap-2">
        <Quadro rotulo="Tempo medido" valor={duracaoHoras(medidoMs)} />
        <Quadro rotulo="Tempo disponível" valor={duracaoHoras(disponivelMs)} />
        <Quadro rotulo="Tempo rodando" valor={duracaoHoras(rodandoMs)} />
        <Quadro rotulo="Tempo paradas internas" valor={duracaoHoras(internoMs)} />
        <Quadro rotulo="Tempo paradas externas" valor={duracaoHoras(externoMs)} />
        {semComunicacaoMs > 0 && <Quadro rotulo="Tempo sem comunicação (fora do OEE)" valor={duracaoHoras(semComunicacaoMs)} />}
        <Quadro rotulo="Paradas internas" valor={String(eventosInternas)} />
        <Quadro rotulo="Paradas externas" valor={String(eventosExternas)} />
        <Quadro rotulo="Produção total" valor={`${inteiro(producaoTotal)} garrafas`} />
        {producaoSemComunicacao > 0 && <Quadro rotulo="Produção sem comunicação" valor={`${inteiro(producaoSemComunicacao)} garrafas`} />}
        {producaoSemComunicacao > 0 && <Quadro rotulo="Produção considerada no OEE" valor={`${inteiro(sessao.producao)} garrafas`} />}
        <Quadro rotulo="MTTR" valor={sessao.mttrMs != null ? duracaoHoras(sessao.mttrMs) : '—'} />
        <Quadro rotulo="MTBF" valor={sessao.mtbfMs != null ? duracaoHoras(sessao.mtbfMs) : '—'} />
      </div>
    </section>
  )
}
