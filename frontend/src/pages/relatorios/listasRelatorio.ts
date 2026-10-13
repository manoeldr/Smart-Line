// Listas lembradas por cliente para os campos digitados do relatório (Produto e Responsável
// do Cliente): o que foi usado ao exportar fica sugerido na próxima vez, e cada item pode ser
// removido. Por enquanto ficam neste navegador (localStorage); vão para o banco quando o
// backend do relatório existir, sem mudar quem usa estas funções.
export type ListaRelatorio = 'produtos' | 'responsaveisCliente' | 'emailsCliente'

function chave(clienteId: string, lista: ListaRelatorio) {
  return `smartline.relatorio.${lista}.${clienteId}`
}

export function lerLista(clienteId: string, lista: ListaRelatorio): string[] {
  try {
    const bruto = localStorage.getItem(chave(clienteId, lista))
    const valores = bruto ? JSON.parse(bruto) : []
    return Array.isArray(valores) ? valores.filter(v => typeof v === 'string') : []
  } catch {
    return []
  }
}

function gravar(clienteId: string, lista: ListaRelatorio, valores: string[]) {
  try { localStorage.setItem(chave(clienteId, lista), JSON.stringify(valores)) } catch { /* sem armazenamento: só não lembra */ }
}

// Guarda o valor no topo da lista (sem repetir; comparação sem diferenciar maiúsculas).
export function lembrarNaLista(clienteId: string, lista: ListaRelatorio, valor: string): string[] {
  const limpo = valor.trim()
  if (!limpo) return lerLista(clienteId, lista)
  const atuais = lerLista(clienteId, lista).filter(v => v.toLowerCase() !== limpo.toLowerCase())
  const novos = [limpo, ...atuais].slice(0, 50)
  gravar(clienteId, lista, novos)
  return novos
}

export function removerDaLista(clienteId: string, lista: ListaRelatorio, valor: string): string[] {
  const novos = lerLista(clienteId, lista).filter(v => v !== valor)
  gravar(clienteId, lista, novos)
  return novos
}
