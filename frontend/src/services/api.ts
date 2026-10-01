const BASE_URL = '/api'

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const token = localStorage.getItem('token')

  // no-store: os dados mudam a cada gravação e o navegador não pode reaproveitar uma resposta
  // antiga (ex.: a página HTML que o backend devolvia antes de um endereço novo existir).
  const res = await fetch(`${BASE_URL}${path}`, {
    cache: 'no-store',
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options?.headers,
    },
  })

  // Login expirado (o token vale 8 h) ou inválido: volta para o login, guardando onde estava
  // para retornar depois de entrar. O próprio login responde 401 com senha errada: esse segue.
  if (res.status === 401 && path !== '/auth/login') {
    sairPorSessaoExpirada()
    // Nunca resolve: a página vai ser trocada; assim nenhuma tela mostra erro no meio do caminho.
    return new Promise<T>(() => {})
  }

  if (!res.ok) {
    const erro = await res.text()
    throw new Error(erro || `Erro ${res.status}`)
  }

  if (res.status === 204) return undefined as T
  return res.json()
}

// Chave com o endereço a voltar depois do login (usada pela tela de login).
export const CHAVE_VOLTAR_APOS_LOGIN = 'smartline.voltarAposLogin'

let saindo = false
function sairPorSessaoExpirada() {
  if (saindo || window.location.pathname === '/login') return
  saindo = true
  try {
    sessionStorage.setItem(CHAVE_VOLTAR_APOS_LOGIN, window.location.pathname + window.location.search)
  } catch { /* sem armazenamento: volta para o Overview */ }
  localStorage.removeItem('token')
  localStorage.removeItem('clienteId')
  window.location.replace('/login?expirou=1')
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
