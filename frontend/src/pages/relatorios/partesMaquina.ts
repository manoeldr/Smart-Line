// Com 12 pt a máquina não cabe numa folha: quais folhas ela gera, dados os blocos ligados.
import type { Bloco } from './blocos'

export type ParteMaquina = 'indicadores' | 'paradas'

export function partesDaMaquina(blocos: Bloco[]): ParteMaquina[] {
  const partes: ParteMaquina[] = []
  // 1ª folha: indicadores, produção por hora e paradas por motivo; 2ª folha: produção
  // acumulada × esperada e paradas por hora
  if (blocos.includes('indicadores') || blocos.includes('producaoHora') || blocos.includes('paradasMotivo')) partes.push('indicadores')
  if (blocos.includes('producaoAcumulada') || blocos.includes('paradasHora')) partes.push('paradas')
  return partes
}

// Os blocos da 2ª folha
export const BLOCOS_SEGUNDA_FOLHA: Bloco[] = ['producaoAcumulada', 'paradasHora']

// Com os dois gráficos na 2ª folha não sobra lugar para a linha do tempo começar nela
export function graficosNaSegundaFolha(blocos: Bloco[]): number {
  return BLOCOS_SEGUNDA_FOLHA.filter(b => blocos.includes(b)).length
}
