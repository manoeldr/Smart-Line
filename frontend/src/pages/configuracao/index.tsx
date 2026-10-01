// Tela de Configurações — navegação por abas.
// Visibilidade das abas por nível: Administrador e Desenvolvedor veem tudo; Auditor só vê Clientes (modo restrito) e Máquinas.
// Dispositivos IoT (WISE do Semi Automático) é só Administrador e Desenvolvedor, como no backend.
import { useSearchParams } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import AbaUsuarios from './AbaUsuarios'
import AbaClientes from './AbaClientes'
import AbaMaquinas from './AbaMaquinas'
import AbaExportImport from './AbaExportImport'
import AbaDispositivosIot from './AbaDispositivosIot'
import { tabButton } from '../../styles/tables'

type Aba = 'usuarios' | 'clientes' | 'maquinas' | 'dispositivos' | 'exportimport'

export default function Configuracao() {
  const { usuario } = useAuth()
  const nivel = usuario?.nivel ?? ''

  // Aba na URL (?aba=) para o F5 voltar nela. Trocar de aba limpa o resto (modais da outra aba).
  const [params, setParams] = useSearchParams()

  const abas: { id: Aba; label: string; niveis: string[] }[] = [
    { id: 'usuarios', label: 'Usuários', niveis: ['Administrador', 'Desenvolvedor'] },
    { id: 'clientes', label: 'Clientes', niveis: ['Administrador', 'Desenvolvedor', 'Auditor'] },
    { id: 'maquinas', label: 'Máquinas', niveis: ['Administrador', 'Desenvolvedor', 'Auditor'] },
    { id: 'dispositivos', label: 'Dispositivos IoT', niveis: ['Administrador', 'Desenvolvedor'] },
    { id: 'exportimport', label: 'Exportar/Importar', niveis: ['Administrador', 'Desenvolvedor'] },
  ]

  const abasVisiveis = abas.filter(a => a.niveis.includes(nivel))
  const abaUrl = params.get('aba')
  const abaAtiva: Aba = abasVisiveis.find(a => a.id === abaUrl)?.id ?? (nivel === 'Auditor' ? 'clientes' : 'usuarios')
  const setAbaAtiva = (aba: Aba) => setParams({ aba }, { replace: true })

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
        {abaAtiva === 'dispositivos' && <AbaDispositivosIot />}
        {abaAtiva === 'exportimport' && <AbaExportImport />}
      </div>
    </div>
  )
}