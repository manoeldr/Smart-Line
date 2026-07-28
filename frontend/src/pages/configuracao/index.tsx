// Tela de Configurações — navegação por abas.
// Visibilidade das abas por nível: SuperAdmin vê tudo; Auditor só vê Clientes (modo restrito) e Máquinas.
import { useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import AbaUsuarios from './AbaUsuarios'
import AbaClientes from './AbaClientes'
import AbaMaquinas from './AbaMaquinas'
import AbaExportImport from './AbaExportImport'
import { tabButton } from '../../styles/tables'

type Aba = 'usuarios' | 'clientes' | 'maquinas' | 'exportimport'

export default function Configuracao() {
  const { usuario } = useAuth()
  const nivel = usuario?.nivel ?? ''

  const [abaAtiva, setAbaAtiva] = useState<Aba>(() => {
    if (nivel === 'Auditor') return 'clientes'
    return 'usuarios'
  })

  const abas: { id: Aba; label: string; niveis: string[] }[] = [
    { id: 'usuarios', label: 'Usuários', niveis: ['SuperAdmin'] },
    { id: 'clientes', label: 'Clientes', niveis: ['SuperAdmin', 'Auditor'] },
    { id: 'maquinas', label: 'Máquinas', niveis: ['SuperAdmin', 'Auditor'] },
    { id: 'exportimport', label: 'Exportar/Importar', niveis: ['SuperAdmin'] },
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
      <div className="border-b border-zinc-200 dark:border-zinc-800 flex">
        {abasVisiveis.map(aba => (
          <button key={aba.id} onClick={() => setAbaAtiva(aba.id)} className={tabButton(abaAtiva === aba.id)}>
            {aba.label}
          </button>
        ))}
      </div>
      <div className="flex-1 overflow-auto p-6">
        {abaAtiva === 'usuarios' && <AbaUsuarios />}
        {abaAtiva === 'clientes' && <AbaClientes />}
        {abaAtiva === 'maquinas' && <AbaMaquinas />}
        {abaAtiva === 'exportimport' && <AbaExportImport />}
      </div>
    </div>
  )
}