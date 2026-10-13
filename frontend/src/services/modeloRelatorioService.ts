// Folha timbrada do relatório para o cliente (Configurações → Relatório): uma por instalação.
import { api, fetchAutenticado, mensagemErro } from './api'

const BASE_URL = '/api/configuracao/modelo-relatorio'

export interface MargensRelatorio {
  superior: number
  inferior: number
  esquerda: number
  direita: number
}

export interface ModeloRelatorioDto {
  existe: boolean
  nomeArquivo: string | null
  importadoEm: string | null
  // Muda a cada importação: entra na URL da imagem para o navegador não reaproveitar a antiga
  versao: number
  margens: MargensRelatorio
}

export const modeloRelatorioService = {
  obter: () => api.get<ModeloRelatorioDto>('/configuracao/modelo-relatorio'),

  // A imagem como URL de blob (a tag <img> não manda o token; o fetch autenticado manda).
  // Quem chama revoga a URL quando não precisar mais.
  carregarImagem: async (versao: number): Promise<string | null> => {
    const res = await fetchAutenticado(`${BASE_URL}/imagem?v=${versao}`)
    if (res.status === 404) return null
    if (!res.ok) throw new Error(`Erro ${res.status} ao carregar a folha`)
    return URL.createObjectURL(await res.blob())
  },

  importar: async (arquivo: File): Promise<ModeloRelatorioDto> => {
    const formData = new FormData()
    formData.append('arquivo', arquivo)
    const res = await fetchAutenticado(BASE_URL, { method: 'POST', body: formData })
    if (!res.ok) throw new Error(mensagemErro(new Error(await res.text()), `Erro ${res.status} ao importar a folha`))
    return res.json()
  },

  atualizarMargens: (margens: MargensRelatorio) =>
    api.put<ModeloRelatorioDto>('/configuracao/modelo-relatorio/margens', margens),

  remover: () => api.delete<void>('/configuracao/modelo-relatorio'),
}
