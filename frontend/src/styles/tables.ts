// Estilos de tabelas de listagem (usadas nas abas de Configurações: Usuários, Clientes, Máquinas).

export const table = 'w-full text-xs'
export const tableHeadRow = 'border-b border-borda'
export const tableHeadCell = 'text-left py-2 pr-4 text-texto-3 font-medium'
export const tableBodyRow = 'border-b border-borda-suave'
export const tableBodyCell = 'py-2.5 pr-4 text-texto'
export const tableBodyCellMuted = 'py-2.5 pr-4 text-texto-2' // texto secundário (ex: estado, descrição)
export const tableActionsCell = 'py-2.5 flex items-center justify-end gap-2' // coluna de botões editar/excluir

// Botão de aba (usado na navegação por abas da tela de Configurações)
export const tabButton = (ativo: boolean) =>
  `px-5 py-3 text-xs font-medium border-b-2 transition-colors ${
    ativo
      ? 'border-accent-600 text-accent-600 dark:text-accent-400'
      : 'border-transparent text-texto-2 hover:text-texto'
  }`