// Estilos de botões reutilizáveis em todo o sistema.
// Use estes em vez de escrever classes Tailwind inline nos componentes.

// Botão primário (ação principal: Salvar, Confirmar, Adicionar)
export const btnPrimary = 'h-9 px-4 bg-blue-600 hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed text-white text-sm font-medium transition-colors'
export const btnPrimarySm = 'h-8 px-3 bg-blue-600 hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed text-white text-xs font-medium transition-colors'
export const btnPrimaryXs = 'h-7 px-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed text-white text-[10px] font-medium transition-colors'

// Botão secundário (Cancelar, Fechar)
export const btnSecondary = 'h-9 px-4 border border-zinc-200 dark:border-zinc-700 text-sm text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800 disabled:opacity-50 transition-colors'
export const btnSecondarySm = 'h-8 px-3 border border-zinc-200 dark:border-zinc-700 text-xs text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800 disabled:opacity-50 transition-colors'

// Botão de ação destrutiva (Excluir, Remover)
export const btnDanger = 'h-9 px-4 border border-red-200 dark:border-red-800 text-sm text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-950 disabled:opacity-50 transition-colors'
export const btnDangerSm = 'h-8 px-3 border border-red-200 dark:border-red-800 text-xs text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-950 disabled:opacity-50 transition-colors'

// Botões de ícone sem fundo (usados em tabelas: editar, excluir)
export const btnIcon = 'text-zinc-400 hover:text-zinc-900 dark:hover:text-zinc-100 transition-colors'
export const btnIconDanger = 'text-zinc-400 hover:text-red-500 transition-colors'

// Botão de alternância (toggle) — usado em seletores tipo "Manual/Semi/Auto" ou "Interna/Externa".
// Recebe se está ativo e a cor de destaque quando ativo.
export function btnToggle(ativo: boolean, corAtiva: 'blue' | 'amber' = 'blue') {
  const cores = {
    blue: 'bg-blue-600 text-white border-blue-600',
    amber: 'bg-amber-500 text-white border-amber-500',
  }
  return `h-9 text-xs font-medium border transition-colors disabled:opacity-40 disabled:cursor-not-allowed ${
    ativo ? cores[corAtiva] : 'border-zinc-200 dark:border-zinc-700 text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800'
  }`
}