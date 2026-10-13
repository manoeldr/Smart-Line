// Relatório para o cliente: PDF gerado no servidor (sem a caixa de impressão).
import { fetchAutenticado, mensagemErro } from './api'

export interface ParametrosRelatorio {
  linhaId: string
  // "yyyy-mm-dd"
  data: string
  produto: string
  responsavelCliente: string
}

export const relatorioService = {
  // Baixa o PDF pelo navegador (o nome vem do servidor: SmartLineScan_[Cliente]_[Linha]_[data].pdf)
  baixarPdf: async (p: ParametrosRelatorio): Promise<void> => {
    const res = await fetchAutenticado('/api/relatorios/pdf', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(p),
    })
    if (!res.ok) throw new Error(mensagemErro(new Error(await res.text()), `Erro ${res.status} ao gerar o PDF`))
    const blob = await res.blob()
    const nome = res.headers.get('content-disposition')?.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/)?.[1] ?? 'relatorio.pdf'
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = decodeURIComponent(nome)
    document.body.appendChild(a)
    a.click()
    a.remove()
    URL.revokeObjectURL(url)
  },
}
