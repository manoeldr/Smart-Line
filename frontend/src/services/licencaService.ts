// Serviço de licença — chamado sem token, já que a verificação acontece antes do login.
const BASE_URL = '/api/licenca'

export interface LicencaStatusDto {
  ativa: boolean
  macAddress: string
}

export const licencaService = {
  getStatus: async (): Promise<LicencaStatusDto> => {
    const res = await fetch(`${BASE_URL}/status`)
    if (!res.ok) throw new Error(`Erro ${res.status} ao verificar licença`)
    return res.json()
  },

  ativar: async (chave: string): Promise<void> => {
    const res = await fetch(`${BASE_URL}/ativar`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ chave }),
    })
    if (!res.ok) {
      const data = await res.json().catch(() => null)
      throw new Error(data?.mensagem || 'Chave inválida')
    }
  },
}