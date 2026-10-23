// Ponte com o aplicativo do computador (SmartLine.Desktop). Só existe quando a tela está
// aberta dentro do aplicativo — no navegador comum nada disso existe, e as telas que usam
// precisam checar `noDesktop()` antes.
//
// É por aqui que a tela pede coisas que só o computador faz: gravar o PDF, abrir o e-mail e
// dizer se este computador é o servidor ou uma estação. Esse último ponto é importante: em
// uma estação, as telas vêm do servidor, então pedir pela API configuraria o servidor, não a
// estação. Pela ponte, quem recebe o pedido é sempre o aplicativo deste computador.

/** Como este computador está configurado agora (o aplicativo informa ao abrir a tela). */
export interface SituacaoDesktop {
  /** true = estação (usa o servidor de outro computador); false = este PC é o servidor */
  modoCliente: boolean
  /** Endereço do servidor configurado (vazio quando este PC é o servidor) */
  endereco: string
  /** Porta usada pelo sistema na rede */
  porta: number
  /** Endereços deste computador na rede, para informar às estações */
  ips: string[]
}

declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage: (mensagem: unknown) => void
        addEventListener: (tipo: 'message', ouvinte: (e: { data: unknown }) => void) => void
        removeEventListener: (tipo: 'message', ouvinte: (e: { data: unknown }) => void) => void
      }
    }
    smartlineDesktop?: SituacaoDesktop
  }
}

/** A tela está aberta dentro do aplicativo do computador? */
export function noDesktop(): boolean {
  return typeof window !== 'undefined' && !!window.chrome?.webview
}

/** Situação deste computador, ou nulo quando a tela não está no aplicativo. */
export function situacaoDesktop(): SituacaoDesktop | null {
  return window.smartlineDesktop ?? null
}

/** Manda um pedido para o aplicativo. Sem aplicativo, não faz nada. */
export function pedirAoDesktop(mensagem: unknown) {
  window.chrome?.webview?.postMessage(mensagem)
}

/**
 * Ouve as respostas do aplicativo. Devolve a função de parar de ouvir — use no retorno do
 * useEffect. Sem aplicativo, devolve uma função vazia e nunca chama o ouvinte.
 */
export function ouvirDesktop(ouvinte: (resposta: RespostaDesktop) => void): () => void {
  const webview = window.chrome?.webview
  if (!webview) return () => {}
  function aoReceber(e: { data: unknown }) {
    const resposta = e.data as RespostaDesktop | null
    if (resposta?.tipo) ouvinte(resposta)
  }
  webview.addEventListener('message', aoReceber)
  return () => webview.removeEventListener('message', aoReceber)
}

/** Resposta do aplicativo: sempre tem o tipo; os outros campos dependem do pedido. */
export interface RespostaDesktop {
  tipo: string
  ok?: boolean
  mensagem?: string
  caminho?: string
}
