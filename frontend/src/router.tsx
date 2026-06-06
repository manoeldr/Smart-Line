import { Routes, Route, Navigate } from 'react-router-dom'
import Layout from './components/layout/Layout'
import Overview from './pages/overview'

export default function Router() {
  return (
    <Routes>
      <Route path="/" element={<Layout />}>
        <Route index element={<Navigate to="/overview" replace />} />
        <Route path="overview" element={<Overview />} />
      </Route>
    </Routes>
  )
}
