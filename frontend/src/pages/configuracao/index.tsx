import { useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import AbaUsuarios from './AbaUsuarios'
import AbaClientes from './AbaClientes'
import AbaLinhas from './AbaLinhas'
import AbaMaquinas from './AbaMaquinas'

type Aba = 'usuarios' | 'clientes' | 'linhas' | 'maquinas'

export default function Configuracao() {
  const { usuario } = useAuth()
  const nivel = usuario?.nivel ?? ''

  const [abaAtiva, setAbaAtiva] = useState<Aba>(() => {
    if (nivel === 'Auditor') return 'linhas'
    return 'usuarios'
  })

  const abas: { id: Aba; label: string; niveis: string[] }[] = [
    { id: 'usuarios', label: 'Usuários', niveis: ['SuperAdmin'] },
    { id: 'clientes', label: 'Clientes', niveis: ['SuperAdmin'] },
    { id: 'linhas', label: 'Linhas', niveis: ['SuperAdmin', 'Auditor'] },
    { id: 'maquinas', label: 'Máquinas', niveis: ['SuperAdmin', 'Auditor'] },
  ]

  const abasVisiveis = abas.filter(a => a.niveis.includes(nivel))

  if (abasVisiveis.length === 0) {
    return (
      <div className="flex items-center justify-center h-48 text-sm text-zinc-400">
        Sem permissão para acessar configurações
      </div>
    )
  }

  return (
    <div className="flex flex-col h-full">
      {/* Abas */}
      <div className="border-b border-zinc-200 dark:border-zinc-800 flex">
        {abasVisiveis.map(aba => (
          <button
            key={aba.id}
            onClick={() => setAbaAtiva(aba.id)}
            className={`px-5 py-3 text-xs font-medium border-b-2 transition-colors ${
              abaAtiva === aba.id
                ? 'border-blue-600 text-blue-600 dark:text-blue-400'
                : 'border-transparent text-zinc-500 hover:text-zinc-900 dark:hover:text-zinc-100'
            }`}
          >
            {aba.label}
          </button>
        ))}
      </div>

      {/* Conteúdo */}
      <div className="flex-1 overflow-auto p-6">
        {abaAtiva === 'usuarios' && <AbaUsuarios />}
        {abaAtiva === 'clientes' && <AbaClientes />}
        {abaAtiva === 'linhas' && <AbaLinhas />}
        {abaAtiva === 'maquinas' && <AbaMaquinas />}
      </div>
    </div>
  )
}
