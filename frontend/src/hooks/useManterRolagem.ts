// Mantém a rolagem da página no F5. A área que rola é a do Layout (não a janela), e o
// navegador só restaura a rolagem da janela; além disso, o conteúdo chega depois ("Carregando..."),
// então a página ainda está curta quando o navegador tentaria. Aqui: guarda a posição a cada
// rolagem (por página, nesta aba do navegador) e, ao recarregar, volta nela assim que o
// conteúdo crescer o bastante. Se a pessoa rolar antes disso, para de tentar.
import { useEffect, type RefObject } from 'react'

const PREFIXO = 'smartline.rolagem'
const TEMPO_MAXIMO_MS = 8000

// Por página; em Configurações, também por aba.
function chaveAtual() {
  const aba = new URLSearchParams(window.location.search).get('aba') ?? ''
  return `${PREFIXO}:${window.location.pathname}:${aba}`
}

export function useManterRolagem(ref: RefObject<HTMLElement | null>) {
  useEffect(() => {
    const el = ref.current
    if (!el) return

    let alvo = 0
    try { alvo = Number(sessionStorage.getItem(chaveAtual())) || 0 } catch { /* sem armazenamento */ }

    // Restaura: tenta a cada 100 ms até chegar na posição, a pessoa rolar ou dar o tempo.
    let restaurando = alvo > 0
    const parar = () => { restaurando = false }
    const inicio = Date.now()
    const intervalo = setInterval(() => {
      if (!restaurando || Date.now() - inicio > TEMPO_MAXIMO_MS) {
        clearInterval(intervalo)
        return
      }
      el.scrollTop = alvo
      if (Math.abs(el.scrollTop - alvo) < 2) parar()
    }, 100)

    // Guarda: a posição de cada página, um pouco depois de parar de rolar.
    let espera: ReturnType<typeof setTimeout> | undefined
    const aoRolar = () => {
      if (restaurando) return
      clearTimeout(espera)
      espera = setTimeout(() => {
        try { sessionStorage.setItem(chaveAtual(), String(Math.round(el.scrollTop))) } catch { /* sem armazenamento */ }
      }, 150)
    }

    el.addEventListener('scroll', aoRolar, { passive: true })
    el.addEventListener('wheel', parar, { passive: true })
    el.addEventListener('touchstart', parar, { passive: true })
    el.addEventListener('keydown', parar)
    el.addEventListener('mousedown', parar)
    return () => {
      clearInterval(intervalo)
      clearTimeout(espera)
      el.removeEventListener('scroll', aoRolar)
      el.removeEventListener('wheel', parar)
      el.removeEventListener('touchstart', parar)
      el.removeEventListener('keydown', parar)
      el.removeEventListener('mousedown', parar)
    }
  }, [ref])
}
