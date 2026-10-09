// Estilos de cards usados no Overview, Dashboard e outras telas com blocos de informação.

// Card básico sem padding (quando o conteúdo interno controla o próprio espaçamento)
export const cardBase = 'bg-superficie border border-borda'
export const cardPadded = 'bg-superficie border border-borda p-4'
export const cardPaddedSm = 'bg-superficie border border-borda p-3'

// Borda superior azul para destacar máquinas/itens marcados como "crítica"
export function cardCritica(critica: boolean) {
  return critica ? 'border-t-2 border-t-accent-600' : ''
}

// Cabeçalho de card com fundo levemente diferenciado (ex: header de LinhaCard)
export const cardHeader = 'px-4 py-2.5 bg-superficie-2 border-b border-borda flex items-center justify-between'

// Caixinha de métrica individual (usada em grids de indicadores — Dashboard, TelaMedicao)
export const metricaBox = 'bg-superficie-2 p-2.5 text-center'
export const metricaValor = 'text-sm font-medium text-texto'
export const metricaLabel = 'text-[9px] text-texto-3 mt-0.5'