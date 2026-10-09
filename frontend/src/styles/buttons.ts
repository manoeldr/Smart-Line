// Estilos de botões reutilizáveis em todo o sistema.
// Use estes em vez de escrever classes Tailwind inline nos componentes.

// Botão primário (ação principal: Salvar, Confirmar, Adicionar)
export const btnPrimary = 'h-9 px-4 bg-accent-600 hover:bg-accent-700 disabled:opacity-40 disabled:cursor-not-allowed text-accent-fg text-sm font-medium transition-colors'
export const btnPrimarySm = 'h-8 px-3 bg-accent-600 hover:bg-accent-700 disabled:opacity-40 disabled:cursor-not-allowed text-accent-fg text-xs font-medium transition-colors'
export const btnPrimaryXs = 'h-7 px-2 bg-accent-600 hover:bg-accent-700 disabled:opacity-40 disabled:cursor-not-allowed text-accent-fg text-[10px] font-medium transition-colors'

// Botão secundário (Cancelar, Fechar)
export const btnSecondary = 'h-9 px-4 border border-borda-forte text-sm text-texto-2 hover:bg-superficie-2 disabled:opacity-50 transition-colors'
export const btnSecondarySm = 'h-8 px-3 border border-borda-forte text-xs text-texto-2 hover:bg-superficie-2 disabled:opacity-50 transition-colors'

// Botão de ação destrutiva (Excluir, Remover)
export const btnDanger = 'h-9 px-4 border border-red-200 dark:border-red-800 text-sm text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-950 disabled:opacity-50 transition-colors'
export const btnDangerSm = 'h-8 px-3 border border-red-200 dark:border-red-800 text-xs text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-950 disabled:opacity-50 transition-colors'

// Botões de ícone sem fundo (usados em tabelas: editar, excluir)
export const btnIcon = 'text-texto-3 hover:text-texto transition-colors'
export const btnIconDanger = 'text-texto-3 hover:text-red-500 transition-colors'

// Botão de alternância (toggle) — usado em seletores tipo "Manual/Semi/Auto" ou "Interna/Externa".
// Recebe se está ativo e a cor de destaque quando ativo.
export function btnToggle(ativo: boolean, corAtiva: 'blue' | 'amber' = 'blue') {
  const cores = {
    blue: 'bg-accent-600 text-accent-fg border-accent-600',
    amber: 'bg-amber-500 text-white border-amber-500',
  }
  return `h-9 text-xs font-medium border transition-colors disabled:opacity-40 disabled:cursor-not-allowed ${
    ativo ? cores[corAtiva] : 'border-borda-forte text-texto-2 hover:bg-superficie-2'
  }`
}