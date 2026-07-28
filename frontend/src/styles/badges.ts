// Estilos de badges/etiquetas usados para indicar status, tipo ou categoria de um item.

// Badge de ativo/inativo — usado em tabelas de Clientes, Linhas, Máquinas, Usuários, Campos
export function badgeStatus(ativo: boolean) {
  return `px-1.5 py-0.5 text-[10px] ${
    ativo
      ? 'bg-green-50 dark:bg-green-950 text-green-700 dark:text-green-400'
      : 'bg-zinc-100 dark:bg-zinc-800 text-zinc-400'
  }`
}

// Badges de tipo de motivo de parada (Interna = azul, Externa = âmbar)
export const badgeInterna = 'text-[10px] text-blue-600 dark:text-blue-400'
export const badgeExterna = 'text-[10px] text-amber-600 dark:text-amber-400'

// Badge "crítica" em máquinas marcadas como críticas para a linha
export const badgeCritica = 'text-[9px] font-medium text-blue-600 dark:text-blue-400 bg-blue-50 dark:bg-blue-950 px-1.5 py-0.5'

// Badge "fixo" — usado no campo Produção, que é sempre coletado e não pode ser removido
export const badgeFixo = 'text-[10px] px-1.5 py-0.5 bg-blue-50 dark:bg-blue-950 text-blue-600 dark:text-blue-400'

// Badge "(novo)" — indica um item ainda não salvo no banco (staged changes)
export const badgeNovo = 'text-[9px] text-blue-500'

// Badge verde de sessão em andamento (ex: no modal de detalhes da máquina)
export const badgeAtivaVerde = 'text-[10px] px-1.5 py-0.5 bg-green-50 dark:bg-green-950 text-green-700 dark:text-green-400'

// Cores da bolinha de status no Overview, por status da máquina
export const dotColorByStatus: Record<string, string> = {
  Rodando:         'bg-green-600',
  ParadaInterna:   'bg-red-500',
  ParadaExterna:   'bg-yellow-400',
  ParadaPlanejada: 'bg-orange-500',
  SemSessao:       'bg-zinc-400',
}