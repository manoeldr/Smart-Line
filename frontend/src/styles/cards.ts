// Estilos de cards usados no Overview, Dashboard e outras telas com blocos de informação.

// Card básico sem padding (quando o conteúdo interno controla o próprio espaçamento)
export const cardBase = 'bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800'
export const cardPadded = 'bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 p-4'
export const cardPaddedSm = 'bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 p-3'

// Borda superior azul para destacar máquinas/itens marcados como "crítica"
export function cardCritica(critica: boolean) {
  return critica ? 'border-t-2 border-t-blue-600' : ''
}

// Cabeçalho de card com fundo levemente diferenciado (ex: header de LinhaCard)
export const cardHeader = 'px-4 py-2.5 bg-zinc-50 dark:bg-zinc-800/50 border-b border-zinc-200 dark:border-zinc-800 flex items-center justify-between'

// Caixinha de métrica individual (usada em grids de indicadores — Dashboard, TelaMedicao)
export const metricaBox = 'bg-zinc-50 dark:bg-zinc-800 p-2.5 text-center'
export const metricaValor = 'text-sm font-medium text-zinc-900 dark:text-zinc-100'
export const metricaLabel = 'text-[9px] text-zinc-400 mt-0.5'