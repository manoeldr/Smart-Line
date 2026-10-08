// Área do Desenvolvedor — só o nível Desenvolvedor (o menu só mostra para ele; o backend
// recusa os demais). Abas: Logs (as últimas linhas do backend, sem terminal) e Simulador
// (WISE simulados dentro do próprio backend, para testar o Semi Automático sem hardware) e
// Coleta (broker e o que o motor vê de cada máquina em medição). Licença (gerar chave) só
// em desenvolvimento.
import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import AbaLogs from './AbaLogs'
import AbaSimulador from './AbaSimulador'
import AbaColeta from './AbaColeta'
import AbaLicenca from './AbaLicenca'
import { desenvolvedorService } from '../../services/desenvolvedorService'
import { tabButton } from '../../styles/tables'

type Aba = 'logs' | 'coleta' | 'simulador' | 'licenca'

const ABAS: { id: Aba; label: string }[] = [
  { id: 'logs', label: 'Logs' },
  { id: 'coleta', label: 'Coleta' },
  { id: 'simulador', label: 'Simulador de WISE' },
  { id: 'licenca', label: 'Licença' },
]

export default function Desenvolvedor() {
  const { usuario } = useAuth()

  // Aba na URL (?aba=) para o F5 voltar nela; cada aba guarda os próprios filtros na URL.
  const [params, setParams] = useSearchParams()

  // A aba Licença só existe com o backend em desenvolvimento (o pacote do cliente não a tem).
  const [comLicenca, setComLicenca] = useState(false)
  useEffect(() => {
    let ativo = true
    desenvolvedorService.licenca().then(() => { if (ativo) setComLicenca(true) }).catch(() => {})
    return () => { ativo = false }
  }, [])
  const abas = ABAS.filter(a => a.id !== 'licenca' || comLicenca)

  const abaUrl = params.get('aba')
  const abaAtiva: Aba = abas.find(a => a.id === abaUrl)?.id ?? 'logs'

  if (usuario?.nivel !== 'Desenvolvedor') {
    return (
      <div className="flex items-center justify-center h-48 text-sm text-zinc-400">
        Área reservada ao Desenvolvedor
      </div>
    )
  }

  return (
    <div className="flex flex-col h-full">
      <div className="border-b border-zinc-200 dark:border-zinc-800 flex">
        {abas.map(aba => (
          <button key={aba.id} onClick={() => setParams({ aba: aba.id }, { replace: true })} className={tabButton(abaAtiva === aba.id)}>
            {aba.label}
          </button>
        ))}
      </div>
      <div className="flex-1 overflow-auto p-6">
        {abaAtiva === 'logs' && <AbaLogs />}
        {abaAtiva === 'coleta' && <AbaColeta />}
        {abaAtiva === 'simulador' && <AbaSimulador />}
        {abaAtiva === 'licenca' && <AbaLicenca />}
      </div>
    </div>
  )
}
