// Com 12 pt a máquina não cabe numa folha: quais folhas ela gera, dados os blocos ligados.
import type { Bloco } from './blocos'

export type ParteMaquina = 'indicadores' | 'paradas'

export function partesDaMaquina(blocos: Bloco[]): ParteMaquina[] {
  const partes: ParteMaquina[] = []
  // 1ª folha: indicadores, produção por hora e paradas por motivo; 2ª folha: paradas por hora
  if (blocos.includes('indicadores') || blocos.includes('producaoHora') || blocos.includes('paradasMotivo')) partes.push('indicadores')
  if (blocos.includes('paradasHora')) partes.push('paradas')
  return partes
}
