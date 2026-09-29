const BASE_URL = '/api'

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const token = localStorage.getItem('token')

  const res = await fetch(`${BASE_URL}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options?.headers,
    },
  })

  if (!res.ok) {
    const erro = await res.text()
    throw new Error(erro || `Erro ${res.status}`)
  }

  if (res.status === 204) return undefined as T
  return res.json()
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body: unknown) => request<T>(path, { method: 'POST', body: JSON.stringify(body) }),
  put: <T>(path: string, body: unknown) => request<T>(path, { method: 'PUT', body: JSON.stringify(body) }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
  patch: <T>(path: string, body?: unknown) => request<T>(path, { method: 'PATCH', body: body ? JSON.stringify(body) : undefined }),
}

// Texto para mostrar ao usuário a partir de um erro da API. O backend devolve
// recusas como { "mensagem": "..." }; o request acima joga esse corpo cru no Error.
export function mensagemErro(e: unknown, padrao: string): string {
  if (!(e instanceof Error) || !e.message) return padrao
  if (e.message === 'Erro 403') return 'Seu usuário não tem permissão para esta ação.'
  try {
    const corpo = JSON.parse(e.message)
    if (typeof corpo?.mensagem === 'string') return corpo.mensagem
  } catch {
    // não era JSON: segue com a mensagem como veio
  }
  return e.message.startsWith('<') || e.message.startsWith('{') ? padrao : e.message
}
