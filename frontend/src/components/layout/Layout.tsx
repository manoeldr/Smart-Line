import { Outlet } from 'react-router-dom'
import { useState } from 'react'
import Sidebar from './Sidebar'
import Topbar from './Topbar'

export default function Layout() {
  const [dataFiltro, setDataFiltro] = useState<string | null>(null)
  const [filtroOpen, setFiltroOpen] = useState(false)

  return (
    <div className="flex h-screen bg-zinc-100 dark:bg-zinc-950">
      <Sidebar />
      <main className="flex-1 flex flex-col overflow-hidden">
        <Topbar
          titulo="Overview"
          dataFiltro={dataFiltro}
          onOpenFiltro={() => setFiltroOpen(true)}
        />
        <div className="flex-1 overflow-y-auto">
          <Outlet context={{ dataFiltro, setDataFiltro, filtroOpen, setFiltroOpen }} />
        </div>
      </main>
    </div>
  )
}
