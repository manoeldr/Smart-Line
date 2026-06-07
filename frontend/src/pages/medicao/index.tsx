import { useState } from 'react'
import type { Linha, MaquinaLinha } from '../../types'
import { sessaoService, type SessaoDto } from '../../services/sessaoService'
import SelecaoMaquina from './SelecaoMaquina'
import TelaMedicao from './TelaMedicao'

interface SessaoAtiva {
  maquina: MaquinaLinha
  linha: Linha
  sessao: SessaoDto
  leiturasIniciais: Record<string, number>
}

export default function Medicao() {
  const [sessaoAtiva, setSessaoAtiva] = useState<SessaoAtiva | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function handleIniciar(
    maquina: MaquinaLinha,
    linha: Linha,
    leiturasIniciais: Record<string, number>
  ) {
    setErro(null)
    setLoading(true)
    try {
      const sessao = await sessaoService.abrir(maquina.id)
      setSessaoAtiva({ maquina, linha, sessao, leiturasIniciais })
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao iniciar medição')
    } finally {
      setLoading(false)
    }
  }

  function handleFinalizar() {
    setSessaoAtiva(null)
  }

  if (sessaoAtiva) {
    return (
      <TelaMedicao
        maquina={sessaoAtiva.maquina}
        linha={sessaoAtiva.linha}
        sessao={sessaoAtiva.sessao}
        leiturasIniciais={sessaoAtiva.leiturasIniciais}
        onFinalizar={handleFinalizar}
      />
    )
  }

  return (
    <>
      {erro && (
        <div className="max-w-lg mx-auto mt-4 px-4">
          <div className="bg-red-50 dark:bg-red-950 border border-red-200 dark:border-red-800 rounded-md px-3 py-2 text-xs text-red-600 dark:text-red-400">
            {erro}
          </div>
        </div>
      )}
      <SelecaoMaquina onIniciar={handleIniciar} loading={loading} />
    </>
  )
}
