import { Routes, Route, Navigate } from 'react-router-dom'
import Layout from './components/layout/Layout'
import ProtectedRoute from './components/layout/ProtectedRoute'
import Overview from './pages/overview'
import Login from './pages/login'
import Medicao from './pages/medicao'

export default function Router() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route element={<ProtectedRoute />}>
        <Route path="/" element={<Layout />}>
          <Route index element={<Navigate to="/overview" replace />} />
          <Route path="overview" element={<Overview />} />
          <Route path="medicao" element={<Medicao />} />
        </Route>
      </Route>
    </Routes>
  )
}