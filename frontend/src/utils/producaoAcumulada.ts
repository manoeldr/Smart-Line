// Produção acumulada × produção nominal esperada, hora a hora (visão Linha e relatório).
// A acumulada soma a produção bruta (normal + sem comunicação) até o fim de cada hora; a
// esperada é a velocidade nominal vezes o tempo decorrido desde o início da sessão até o fim
// da hora (ou até agora / o fim da sessão, na última hora).

export interface PontoProducaoBase {
  hora: string
  quantidade: number
  semComunicacao?: number
  parcial?: boolean
}

export interface PontoAcumulado {
  hora: string
  acumulada: number
  esperada: number
  parcial: boolean
}

// Azul da produção e vermelho da nominal, os mesmos dos outros gráficos
export const COR_ACUMULADA = '#1961ac'
export const COR_ESPERADA = '#ef4444'

export function producaoAcumulada(
  pontos: PontoProducaoBase[],
  inicio: string,
  fim: string | null,
  velocidadeNominal: number,
  agora: Date = new Date(),
): PontoAcumulado[] {
  const inicioMs = new Date(inicio).getTime()
  const limiteMs = fim ? new Date(fim).getTime() : agora.getTime()
  const nominal = Number(velocidadeNominal) || 0
  let acumulada = 0
  return [...pontos]
    .sort((a, b) => new Date(a.hora).getTime() - new Date(b.hora).getTime())
    .map(p => {
      acumulada += p.quantidade + (p.semComunicacao ?? 0)
      const fimDaHora = Math.min(new Date(p.hora).getTime() + 3600000, limiteMs)
      const horas = Math.max(0, fimDaHora - inicioMs) / 3600000
      return {
        hora: new Date(p.hora).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }),
        acumulada,
        esperada: Math.round(nominal * horas),
        parcial: p.parcial === true,
      }
    })
}
