// Linha do tempo em barra (Overview e relatório): cores e categorias dos trechos de uma sessão.
// As paradas são separadas pelo que interessa na linha: interna; falta de produto na entrada e
// saída acumulada (os motivos padrão IoT, pelo nome fixo deles em RegrasPadraoIot); as outras
// externas; programada; e os trechos sem comunicação.
import type { SessaoDetalheDto } from '../services/sessaoDetalheService'

export const COR_TIMELINE = {
  rodando: 'rgb(34, 197, 94)',
  interna: 'rgb(220, 38, 38)',
  entrada: 'rgb(217, 119, 6)',
  saida: 'rgb(59, 130, 246)',
  externa: '#f5b800',
  programada: '#8b5cf6',
  semComunicacao: 'rgb(107, 114, 128)',
} as const

export type CategoriaTimeline = keyof typeof COR_TIMELINE

const MOTIVOS_ENTRADA = ['Falta de garrafas na entrada', 'Abaixo do acúmulo mínimo']
const MOTIVOS_SAIDA = ['Acúmulo na saída de garrafas', 'Acúmulo na saída (caixas/pallets)']

export function categoriaDaParada(motivoNome: string | null, motivoTipo: string | null): CategoriaTimeline {
  if (motivoNome && MOTIVOS_ENTRADA.includes(motivoNome)) return 'entrada'
  if (motivoNome && MOTIVOS_SAIDA.includes(motivoNome)) return 'saida'
  if (motivoTipo === 'Planejada') return 'programada'
  if (motivoTipo === 'Externa') return 'externa'
  return 'interna'
}

export interface TrechoTimeline {
  inicio: number
  fim: number
  tipo: CategoriaTimeline
  texto: string
}

export interface TimelineSessao {
  inicio: number
  fim: number
  emAndamento: boolean
  trechos: TrechoTimeline[]
}

export function horaCurta(ms: number) {
  return new Date(ms).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
}

function duracao(ms: number) {
  const min = Math.round(ms / 60000)
  return min < 60 ? `${min} min` : `${Math.floor(min / 60)} h ${String(min % 60).padStart(2, '0')} min`
}

// Trechos de parada e sem comunicação de uma sessão, do início ao fim (ou agora)
export function montarTimeline(d: SessaoDetalheDto, agora: number): TimelineSessao {
  const inicio = new Date(d.inicio).getTime()
  const fim = d.fim ? new Date(d.fim).getTime() : agora
  const trechos: TrechoTimeline[] = []
  for (const e of d.eventos) {
    if (e.tipo !== 'Parada' && e.tipo !== 'SemComunicacao') continue
    const ini = new Date(e.horario).getTime()
    const fimTrecho = e.duracaoMs != null ? ini + e.duracaoMs : fim
    if (e.tipo === 'SemComunicacao') {
      trechos.push({ inicio: ini, fim: fimTrecho, tipo: 'semComunicacao', texto: `Sem comunicação · ${horaCurta(ini)}–${horaCurta(fimTrecho)} · ${duracao(fimTrecho - ini)}` })
    } else {
      const tipo = categoriaDaParada(e.motivoNome, e.motivoTipo)
      const rotuloTipo = e.motivoTipo === 'Planejada' ? 'programada' : e.motivoTipo === 'Externa' ? 'externa' : 'interna'
      trechos.push({ inicio: ini, fim: fimTrecho, tipo, texto: `${e.motivoNome ?? 'Sem motivo'} (${rotuloTipo}) · ${horaCurta(ini)}–${horaCurta(fimTrecho)} · ${duracao(fimTrecho - ini)}` })
    }
  }
  return { inicio, fim, emAndamento: !d.fim, trechos }
}

// Escala de horas cheias que cobre os intervalos dados
export function escalaHoras(intervalos: { inicio: number; fim: number }[]) {
  if (intervalos.length === 0) return { inicioEscala: 0, fimEscala: 1, horas: [] as number[] }
  const inicioEscala = Math.floor(Math.min(...intervalos.map(i => i.inicio)) / 3600000) * 3600000
  const fimEscala = Math.ceil(Math.max(...intervalos.map(i => i.fim)) / 3600000) * 3600000
  const horas: number[] = []
  for (let t = inicioEscala; t <= fimEscala; t += 3600000) horas.push(t)
  return { inicioEscala, fimEscala, horas }
}
