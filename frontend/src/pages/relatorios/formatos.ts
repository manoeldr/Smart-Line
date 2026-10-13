// Formatação dos valores impressos no relatório.

export function dataHoraLonga(iso: string | null | undefined): string {
  if (!iso) return '—'
  const d = new Date(iso)
  return `${d.toLocaleDateString('pt-BR')} às ${d.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}`
}

// "7 h 05 min" a partir de milissegundos
export function duracaoHoras(ms: number | null | undefined): string {
  const min = Number.isFinite(ms) ? Math.max(0, Math.round((ms as number) / 60000)) : 0
  return `${Math.floor(min / 60)} h ${String(min % 60).padStart(2, '0')} min`
}

export function inteiro(n: number | null | undefined): string {
  return (Number.isFinite(n) ? (n as number) : 0).toLocaleString('pt-BR')
}
