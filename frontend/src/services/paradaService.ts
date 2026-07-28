import { api } from './api'

export interface ParadaDto {
  id: string
  sessaoId: string
  motivoId: string | null
  inicio: string
  fim: string | null
  fotoPath: string | null
}

function authHeaders() {
  const token = localStorage.getItem('token')
  return token ? { Authorization: `Bearer ${token}` } : {}
}

export const paradaService = {
  abrir: (sessaoId: string, inicio: Date) =>
    api.post<ParadaDto>('/paradas', { sessaoId, inicio: inicio.toISOString() }),

  fechar: (paradaId: string, motivoId: string, fim: Date) =>
    api.patch<ParadaDto>(`/paradas/${paradaId}/fechar`, { motivoId, fim: fim.toISOString() }),

  // Envia a foto da parada — usa FormData (multipart), diferente das outras chamadas que são JSON
  uploadFoto: async (paradaId: string, arquivo: File): Promise<ParadaDto> => {
    const formData = new FormData()
    formData.append('foto', arquivo)

    const res = await fetch(`/api/paradas/${paradaId}/foto`, {
      method: 'POST',
      headers: authHeaders(),
      body: formData,
    })
    if (!res.ok) throw new Error(`Erro ${res.status} ao enviar foto`)
    return res.json()
  },
}