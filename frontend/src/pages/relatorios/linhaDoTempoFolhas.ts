// Como a linha do tempo se divide em folhas de tamanho fixo (ver LinhaDoTempo.tsx).
import type { EventoTimelineDto } from '../../services/sessaoDetalheService'

// Quantas linhas cabem numa folha A4 depois do título (10 pt: altura de linha ~6 mm)
export const LINHAS_POR_FOLHA = 32
// Quantas cabem na folha de "Paradas por hora", embaixo do gráfico
export const LINHAS_APOS_GRAFICO = 16

// Divide os eventos em fatias: a primeira com o tamanho pedido, as demais com uma folha inteira
export function fatiarEventos<T>(eventos: T[], primeira: number): T[][] {
  const fatias: T[][] = []
  if (eventos.length === 0) return fatias
  fatias.push(eventos.slice(0, primeira))
  for (let k = primeira; k < eventos.length; k += LINHAS_POR_FOLHA) fatias.push(eventos.slice(k, k + LINHAS_POR_FOLHA))
  return fatias
}

// Ordena do primeiro ao último evento (o detalhe na tela mostra o contrário)
export function eventosCronologicos(eventos: EventoTimelineDto[]): EventoTimelineDto[] {
  return [...eventos].sort((a, b) => new Date(a.horario).getTime() - new Date(b.horario).getTime())
}
