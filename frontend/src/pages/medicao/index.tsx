import { useState } from 'react'
import type { Linha, MaquinaLinha } from '../../types'
import SelecaoMaquina from './SelecaoMaquina'

interface SessaoAtiva {
  maquina: MaquinaLinha
  linha: Linha
  leiturasIniciais: Record<string, number>
  inicio: Date
}

export default function Medicao() {
  const [sessao, setSessao] = useState<SessaoAtiva | null>(null)

  function handleIniciar(maquina: MaquinaLinha, linha: Linha, leiturasIniciais: Record<string, number>) {
    setSessao({ maquina, linha, leiturasIniciais, inicio: new Date() })
  }

  if (!sessao) {
    return <SelecaoMaquina onIniciar={handleIniciar} />
  }

  return (
    <div className="p-4">
      <p className="text-sm text-zinc-400">Sessão iniciada — tela de medição em construção...</p>
      <p className="text-xs text-zinc-400 mt-1">Máquina: {sessao.maquina.maquinaNome}</p>
    </div>
  )
}
