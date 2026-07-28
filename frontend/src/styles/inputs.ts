// Estilos de campos de formulário (inputs, selects, labels) reutilizáveis.

// Input padrão pequeno — usado em formulários dentro de modais compactos
export const inputBase = 'w-full h-8 px-2 text-xs border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-zinc-900 dark:text-zinc-100 placeholder:text-zinc-400 focus:outline-none focus:ring-1 focus:ring-blue-500'

// Input médio — usado em telas com mais espaço (filtros, telas de seleção)
export const inputMd = 'h-9 px-3 border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-sm text-zinc-900 dark:text-zinc-100 placeholder:text-zinc-400 focus:outline-none focus:ring-2 focus:ring-blue-500'
export const inputMdFull = 'w-full h-9 px-3 border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-sm text-zinc-900 dark:text-zinc-100 placeholder:text-zinc-400 focus:outline-none focus:ring-2 focus:ring-blue-500'

// Estado de input desabilitado/travado (ex: leitura inicial já salva, campo fixo)
export const inputDisabled = 'bg-blue-50 dark:bg-blue-950 border-blue-200 dark:border-blue-800 text-blue-700 dark:text-blue-300 cursor-not-allowed'

// Labels de campo
export const label = 'text-xs text-zinc-500 mb-1 block'
export const labelSm = 'text-xs text-zinc-500'

// Checkbox — só define a cor de destaque quando marcado
export const checkbox = 'accent-blue-600'