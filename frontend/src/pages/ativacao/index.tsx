// Tela de ativação de licença — aparece no lugar de qualquer outra tela quando
// o sistema detecta que essa máquina ainda não foi licenciada.
// Mostra o MAC address para o usuário enviar ao fornecedor e receber a chave de ativação.
import { useState } from 'react'
import { licencaService } from '../../services/licencaService'
import { btnPrimary } from '../../styles/buttons'
import { inputMdFull, label } from '../../styles/inputs'

interface Props {
  macAddress: string
  onAtivado: () => void
}

export default function Ativacao({ macAddress, onAtivado }: Props) {
  const [chave, setChave] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [ativando, setAtivando] = useState(false)

  async function handleAtivar(e: React.FormEvent) {
    e.preventDefault()
    setErro(null)
    setAtivando(true)
    try {
      await licencaService.ativar(chave)
      onAtivado()
    } catch (err: unknown) {
      setErro(err instanceof Error ? err.message : 'Erro ao ativar licença')
    } finally {
      setAtivando(false)
    }
  }

  return (
    <div className="min-h-screen bg-zinc-100 dark:bg-zinc-950 flex items-center justify-center p-4">
      <div className="w-full max-w-md">
        <div className="flex items-center gap-3 mb-8 justify-center">
          <div className="w-10 h-10 bg-blue-600 rounded-xl flex items-center justify-center flex-shrink-0">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/>
            </svg>
          </div>
          <div>
            <p className="text-lg font-medium text-zinc-900 dark:text-zinc-100">SmartLine</p>
            <p className="text-xs text-zinc-500">Ativação necessária</p>
          </div>
        </div>

        <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 rounded-xl p-6">
          <h1 className="text-sm font-medium text-zinc-900 dark:text-zinc-100 mb-2">Ativar este computador</h1>
          <p className="text-xs text-zinc-500 mb-4">
            Envie o código abaixo ao suporte para receber sua chave de ativação.
          </p>

          <div className="mb-5">
            <label className={label}>Código do computador</label>
            <div className="flex items-center gap-2">
              <code className="flex-1 h-9 px-3 flex items-center border border-zinc-200 dark:border-zinc-700 bg-zinc-50 dark:bg-zinc-800 text-sm text-zinc-900 dark:text-zinc-100 rounded-sm font-mono">
                {macAddress}
              </code>
              <button
                type="button"
                onClick={() => navigator.clipboard.writeText(macAddress)}
                title="Copiar"
                className="h-9 w-9 flex items-center justify-center border border-zinc-200 dark:border-zinc-700 text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800 rounded-sm"
              >
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="9" y="9" width="13" height="13" rx="2" ry="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/>
                </svg>
              </button>
            </div>
          </div>

          <form onSubmit={handleAtivar} className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <label className={label}>Chave de ativação</label>
              <input
                type="text"
                value={chave}
                onChange={e => setChave(e.target.value)}
                placeholder="XXXX-XXXX-XXXX-XXXX-XXXX-XXXX"
                required
                className={`${inputMdFull} rounded-sm font-mono`}
              />
            </div>
            {erro && (
              <p className="text-xs text-red-500 bg-red-50 dark:bg-red-950 px-3 py-2 rounded-sm">
                {erro}
              </p>
            )}
            <button type="submit" disabled={ativando} className={`${btnPrimary} w-full rounded-sm`}>
              {ativando ? 'Ativando...' : 'Ativar'}
            </button>
          </form>
        </div>
      </div>
    </div>
  )
}