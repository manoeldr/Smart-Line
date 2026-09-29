// Card de resumo de OEE por máquina no Dashboard, agregando dados de todas as sessões
// finalizadas dentro do período selecionado. Ao clicar, abre o MaquinaDetalheModal.
import type { MaquinaDashboardDto } from '../../services/dashboardService'
import { badgeCritica } from '../../styles/badges'
import { cardPadded } from '../../styles/cards'

interface Props {
  dados: MaquinaDashboardDto
}

function formatarHoras(ms: number) {
  const horas = ms / 3600000
  return horas.toFixed(1) + 'h'
}

export default function MaquinaDashboardCard({ dados }: Props) {
  const oeeColor = dados.oee >= 85
    ? 'text-green-600 dark:text-green-400'
    : dados.oee >= 60
      ? 'text-amber-600 dark:text-amber-400'
      : 'text-red-600 dark:text-red-400'

  return (
    // Crítica: faixa azul no topo desenhada por dentro (sombra, não borda), para não mudar
    // a altura; h-full deixa os cards de uma mesma fileira sempre com a mesma altura.
    <div className={`${cardPadded} relative h-full text-center ${dados.critica ? 'shadow-[inset_0_2px_0_0_#2563eb]' : ''}`}>

      {/* Header: a etiqueta "crítica" fica no canto, fora do fluxo, para os cards
          críticos e os demais ficarem com tudo na mesma altura lado a lado */}
      {dados.critica && <span className={`${badgeCritica} absolute top-3 right-3`}>crítica</span>}
      <p className="text-base font-medium text-zinc-900 dark:text-zinc-100 mb-3 px-14 truncate">{dados.maquinaNome}</p>

      {/* OEE grande */}
      <div className="mb-4">
        <p className={`text-4xl font-medium ${oeeColor}`}>{dados.oee}%</p>
        <p className="text-xs text-zinc-400">OEE médio</p>
      </div>

      {/* Disponibilidade / Performance / Qualidade */}
      <div className="grid grid-cols-3 gap-2 mb-4">
        <Indicador valor={`${dados.disponibilidade}%`} rotulo="Disponib." />
        <Indicador valor={`${dados.performance}%`} rotulo="Perform." />
        <Indicador valor={`${dados.qualidade}%`} rotulo="Qualid." />
      </div>

      {/* Produção / Refugo */}
      <div className="grid grid-cols-2 gap-2 mb-3 pt-3 border-t border-zinc-100 dark:border-zinc-800">
        <Indicador valor={dados.producao.toLocaleString('pt-BR')} rotulo="Produção total" />
        <Indicador valor={dados.refugo.toLocaleString('pt-BR')} rotulo="Refugo total" />
      </div>

      {/* Tempo rodando / parado / sessões */}
      <div className="grid grid-cols-3 gap-2 pt-3 border-t border-zinc-100 dark:border-zinc-800">
        <Indicador valor={formatarHoras(dados.tempoRodandoMs)} rotulo="rodando" />
        <Indicador valor={formatarHoras(dados.tempoParadoMs)} rotulo="parado" />
        <Indicador valor={String(dados.numSessoes)} rotulo="sessões" />
      </div>
    </div>
  )
}

// Valor em destaque com o rótulo embaixo, centralizados no espaço dele.
function Indicador({ valor, rotulo }: { valor: string; rotulo: string }) {
  return (
    <div className="flex flex-col items-center">
      <p className="text-base font-medium text-zinc-900 dark:text-zinc-100">{valor}</p>
      <p className="text-xs text-zinc-400">{rotulo}</p>
    </div>
  )
}
