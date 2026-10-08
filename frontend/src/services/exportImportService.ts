import { fetchAutenticado } from './api'

const BASE_URL = '/api/export-import'

export interface ExportOpcoes {
  clientesLinhas: boolean
  maquinas: boolean
  sessoesMedicoes: boolean
  usuarios: boolean
}

export interface ImportResumoDto {
  clientes: number
  linhas: number
  maquinas: number
  sessoes: number
  usuarios: number
  regras: number
  textosEntradas: number
  wises: number
}

export const exportImportService = {
  // Exportar: baixa um .zip com os dados selecionados
  exportar: async (opcoes: ExportOpcoes) => {
    const res = await fetchAutenticado(`${BASE_URL}/exportar`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(opcoes),
    })
    if (!res.ok) throw new Error(`Erro ${res.status} ao exportar`)

    const blob = await res.blob()
    const nomeArquivo = res.headers.get('content-disposition')?.match(/filename="?(.+?)"?$/)?.[1]
      ?? `smartline-export-${Date.now()}.zip`

    const url = window.URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = nomeArquivo
    document.body.appendChild(a)
    a.click()
    a.remove()
    window.URL.revokeObjectURL(url)
  },

  // Prévia: envia o .zip e recebe a contagem por categoria, sem aplicar no banco
  previa: async (arquivo: File): Promise<ImportResumoDto> => {
    const formData = new FormData()
    formData.append('arquivo', arquivo)

    const res = await fetchAutenticado(`${BASE_URL}/previa`, {
      method: 'POST',
      body: formData,
    })
    if (!res.ok) throw new Error(`Erro ${res.status} ao ler o arquivo`)
    return res.json()
  },

  // Importar: aplica de fato os dados do .zip no banco
  importar: async (arquivo: File): Promise<ImportResumoDto> => {
    const formData = new FormData()
    formData.append('arquivo', arquivo)

    const res = await fetchAutenticado(`${BASE_URL}/importar`, {
      method: 'POST',
      body: formData,
    })
    if (!res.ok) throw new Error(`Erro ${res.status} ao importar`)
    return res.json()
  },
}