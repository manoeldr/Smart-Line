// Blocos que o usuário liga/desliga no relatório (valem para todas as máquinas). A escolha
// fica neste navegador e vai na URL (?blocos=) para o PDF do servidor sair igual.
export type Bloco = 'indicadores' | 'producaoHora' | 'paradasMotivo' | 'paradasHora' | 'linhaTempo'

export const BLOCOS: { id: Bloco; rotulo: string }[] = [
  { id: 'indicadores', rotulo: 'Indicadores' },
  { id: 'producaoHora', rotulo: 'Produção por hora' },
  { id: 'paradasMotivo', rotulo: 'Paradas por motivo' },
  { id: 'paradasHora', rotulo: 'Paradas por hora' },
  { id: 'linhaTempo', rotulo: 'Linha do tempo' },
]

// Padrão: tudo menos a linha do tempo (pode ocupar várias folhas por máquina)
export const BLOCOS_PADRAO: Bloco[] = ['indicadores', 'producaoHora', 'paradasMotivo', 'paradasHora']

const CHAVE = 'smartline.relatorio.blocos'

export function lerBlocos(daUrl: string | null): Bloco[] {
  const ids = new Set(BLOCOS.map(b => b.id))
  const valida = (lista: unknown): Bloco[] | null =>
    Array.isArray(lista) ? lista.filter((b): b is Bloco => typeof b === 'string' && ids.has(b as Bloco)) : null
  if (daUrl !== null) return valida(daUrl.split(',')) ?? []
  try {
    const guardado = localStorage.getItem(CHAVE)
    if (guardado) return valida(JSON.parse(guardado)) ?? BLOCOS_PADRAO
  } catch { /* sem armazenamento */ }
  return BLOCOS_PADRAO
}

export function guardarBlocos(blocos: Bloco[]) {
  try { localStorage.setItem(CHAVE, JSON.stringify(blocos)) } catch { /* sem armazenamento: só não lembra */ }
}
