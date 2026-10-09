// Estilos de estrutura de modais (overlay, container, header, body, footer).
// Todo modal do sistema deve seguir esse padrão visual.

// Fundo escurecido atrás do modal
export const modalOverlay = 'fixed inset-0 z-50 flex items-center justify-center bg-black/40'
export const modalOverlayDark = 'fixed inset-0 z-50 flex items-center justify-center bg-black/50' // mais escuro — usado em modais de alerta (ex: previsão de término)
export const modalOverlayNested = 'fixed inset-0 z-[60] flex items-center justify-center bg-black/40' // z-index maior — para modal aberto sobre outro modal

// Containers de tamanho fixo para modais simples (formulário curto, confirmação)
export const modalContainerSm = 'bg-superficie border border-borda w-72 p-5'
export const modalContainerMd = 'bg-superficie border border-borda w-80 p-5'

// Painel de modal maior, com header/body/footer separados (ex: ConfiguracaoClienteModal)
export const modalPanel = 'bg-superficie border border-borda flex flex-col'

export const modalHeader = 'px-5 py-4 border-b border-borda'
export const modalTitle = 'text-sm font-medium text-texto'
export const modalSubtitle = 'text-xs text-texto-3 mt-0.5'

export const modalBody = 'px-5 py-4 flex flex-col gap-4 overflow-y-auto'
export const modalFooter = 'px-5 py-3 border-t border-borda flex justify-end gap-2'