// Estilos de estrutura de modais (overlay, container, header, body, footer).
// Todo modal do sistema deve seguir esse padrão visual.

// Fundo escurecido atrás do modal
export const modalOverlay = 'fixed inset-0 z-50 flex items-center justify-center bg-black/40'
export const modalOverlayDark = 'fixed inset-0 z-50 flex items-center justify-center bg-black/50' // mais escuro — usado em modais de alerta (ex: previsão de término)
export const modalOverlayNested = 'fixed inset-0 z-[60] flex items-center justify-center bg-black/40' // z-index maior — para modal aberto sobre outro modal

// Containers de tamanho fixo para modais simples (formulário curto, confirmação)
export const modalContainerSm = 'bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 w-72 p-5'
export const modalContainerMd = 'bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 w-80 p-5'

// Painel de modal maior, com header/body/footer separados (ex: ConfiguracaoClienteModal)
export const modalPanel = 'bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 flex flex-col'

export const modalHeader = 'px-5 py-4 border-b border-zinc-200 dark:border-zinc-800'
export const modalTitle = 'text-sm font-medium text-zinc-900 dark:text-zinc-100'
export const modalSubtitle = 'text-xs text-zinc-400 mt-0.5'

export const modalBody = 'px-5 py-4 flex flex-col gap-4 overflow-y-auto'
export const modalFooter = 'px-5 py-3 border-t border-zinc-200 dark:border-zinc-800 flex justify-end gap-2'