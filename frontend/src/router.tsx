import { useState, useEffect } from 'react'
import { Routes, Route, Navigate } from 'react-router-dom'
import Layout from './components/layout/Layout'
import ProtectedRoute from './components/layout/ProtectedRoute'
import Overview from './pages/overview'
import Login from './pages/login'
import Medicao from './pages/medicao'
import Configuracao from './pages/configuracao'
import Dashboard from './pages/dashboard'
import Ativacao from './pages/ativacao'
import { licencaService } from './services/licencaService'

export default function Router() {
  // Verifica se essa máquina está licenciada antes de renderizar qualquer rota —
  // se não estiver, mostra a tela de ativação no lugar de tudo (inclusive o login).
  const [verificando, setVerificando] = useState(true)
  const [licencaAtiva, setLicencaAtiva] = useState(false)
  const [macAddress, setMacAddress] = useState('')

  async function verificarLicenca() {
    try {
      const status = await licencaService.getStatus()
      setLicencaAtiva(status.ativa)
      setMacAddress(status.macAddress)
    } catch {
      setLicencaAtiva(false)
    } finally {
      setVerificando(false)
    }
  }

  useEffect(() => {
    verificarLicenca()
  }, [])

  if (verificando) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-zinc-100 dark:bg-zinc-950">
        <p className="text-sm text-zinc-400">Carregando...</p>
      </div>
    )
  }

  if (!licencaAtiva) {
    return <Ativacao macAddress={macAddress} onAtivado={verificarLicenca} />
  }

  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route element={<ProtectedRoute />}>
        <Route path="/" element={<Layout />}>
          <Route index element={<Navigate to="/overview" replace />} />
          <Route path="overview" element={<Overview />} />
          <Route path="medicao" element={<Medicao />} />
          <Route path="dashboard" element={<Dashboard />} />
          <Route path="configuracao" element={<Configuracao />} />
        </Route>
      </Route>
    </Routes>
  )
}