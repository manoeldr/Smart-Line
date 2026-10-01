// Guarda na URL (?nome=valor) o que está aberto na tela — modal, aba, item em edição —
// para que o F5 volte com tudo como estava. Trocar de página pelo menu limpa os parâmetros.
import { useCallback } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'

/** Lê e grava vários parâmetros de uma vez (nulo ou vazio remove). */
export function useAlterarUrl() {
  const navigate = useNavigate()
  return useCallback((mudancas: Record<string, string | null>) => {
    // Lê a URL de agora (não a do último render): duas alterações seguidas não se perdem.
    const params = new URLSearchParams(window.location.search)
    for (const [nome, valor] of Object.entries(mudancas)) {
      if (valor === null || valor === '') params.delete(nome)
      else params.set(nome, valor)
    }
    const busca = params.toString()
    // replace: abrir e fechar modais não enche o histórico do "Voltar".
    navigate({ pathname: window.location.pathname, search: busca ? `?${busca}` : '' }, { replace: true })
  }, [navigate])
}

/** Um parâmetro da URL como estado: [valor, definir]. */
export function useParametroUrl(nome: string): [string | null, (valor: string | null) => void] {
  const [params] = useSearchParams()
  const alterar = useAlterarUrl()
  const definir = useCallback((valor: string | null) => alterar({ [nome]: valor }), [alterar, nome])
  return [params.get(nome), definir]
}
