// Estilos de campos de formulário (inputs, selects, labels) reutilizáveis.

// Input padrão pequeno — usado em formulários dentro de modais compactos
export const inputBase = 'w-full h-8 px-2 text-xs border border-borda-forte bg-campo text-texto placeholder:text-texto-3 focus:outline-none focus:ring-1 focus:ring-accent-500'

// Input médio — usado em telas com mais espaço (filtros, telas de seleção)
export const inputMd = 'h-9 px-3 border border-borda-forte bg-campo text-sm text-texto placeholder:text-texto-3 focus:outline-none focus:ring-2 focus:ring-accent-500'
export const inputMdFull = 'w-full h-9 px-3 border border-borda-forte bg-campo text-sm text-texto placeholder:text-texto-3 focus:outline-none focus:ring-2 focus:ring-accent-500'

// Estado de input desabilitado/travado (ex: leitura inicial já salva, campo fixo)
export const inputDisabled = 'bg-accent-50 dark:bg-accent-950 border-accent-200 dark:border-accent-800 text-accent-700 dark:text-accent-300 cursor-not-allowed'

// Labels de campo
export const label = 'text-xs text-texto-2 mb-1 block'
export const labelSm = 'text-xs text-texto-2'

// Checkbox — só define a cor de destaque quando marcado
export const checkbox = 'accent-accent-600'