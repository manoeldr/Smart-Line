// Estilos de tabelas de listagem (usadas nas abas de Configurações: Usuários, Clientes, Máquinas).

export const table = 'w-full text-xs'
export const tableHeadRow = 'border-b border-zinc-200 dark:border-zinc-800'
export const tableHeadCell = 'text-left py-2 pr-4 text-zinc-400 font-medium'
export const tableBodyRow = 'border-b border-zinc-100 dark:border-zinc-800'
export const tableBodyCell = 'py-2.5 pr-4 text-zinc-900 dark:text-zinc-100'
export const tableBodyCellMuted = 'py-2.5 pr-4 text-zinc-500' // texto secundário (ex: estado, descrição)
export const tableActionsCell = 'py-2.5 flex items-center justify-end gap-2' // coluna de botões editar/excluir

// Botão de aba (usado na navegação por abas da tela de Configurações)
export const tabButton = (ativo: boolean) =>
  `px-5 py-3 text-xs font-medium border-b-2 transition-colors ${
    ativo
      ? 'border-blue-600 text-blue-600 dark:text-blue-400'
      : 'border-transparent text-zinc-500 hover:text-zinc-900 dark:hover:text-zinc-100'
  }`