// Formatação de instantes vindos da API (UTC, em ISO) para a tela.

// "há 12 s", "há 3 min", "há 2 h"; mais antigo que um dia: data e hora.
export function tempoDesde(iso: string | null | undefined, agora: Date = new Date()): string {
  if (!iso) return '—'
  const segundos = Math.max(0, Math.round((agora.getTime() - new Date(iso).getTime()) / 1000))
  if (segundos < 60) return `há ${segundos} s`
  if (segundos < 3600) return `há ${Math.floor(segundos / 60)} min`
  if (segundos < 86400) return `há ${Math.floor(segundos / 3600)} h`
  return dataHora(iso)
}

export function dataHora(iso: string | null | undefined): string {
  if (!iso) return '—'
  const d = new Date(iso)
  return `${d.toLocaleDateString('pt-BR')} ${d.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}`
}

export function hora(iso: string | null | undefined): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}
