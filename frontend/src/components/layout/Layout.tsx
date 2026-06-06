import { Outlet } from 'react-router-dom'
import { useState } from 'react'
import type React from 'react'
import Sidebar from './Sidebar'
import Topbar from './Topbar'
import DateFilterModal from '../../modals/DateFilterModal'

export default function Layout() {
  const [dataFiltro, setDataFiltro] = useState<string | null>(null)
  const [filtroOpen, setFiltroOpen] = useState(false)
  const [anchorRef, setAnchorRef] = useState<React.RefObject<HTMLButtonElement | null> | null>(null)
  const datasComSessao: string[] = []

  function handleOpenFiltro(ref: React.RefObject<HTMLButtonElement | null>) {
    setAnchorRef(ref)
    setFiltroOpen(prev => !prev)
  }

  return (
    <div className="flex h-screen bg-zinc-100 dark:bg-zinc-950">
      <Sidebar />
      <main className="flex-1 flex flex-col overflow-hidden">
        <Topbar
          titulo="Overview"
          dataFiltro={dataFiltro}
          onOpenFiltro={handleOpenFiltro}
        />
        <div className="flex-1 overflow-y-auto">
          <Outlet context={{ dataFiltro, setDataFiltro, filtroOpen, setFiltroOpen }} />
        </div>
      </main>

      {anchorRef && (
        <DateFilterModal
          open={filtroOpen}
          onClose={() => setFiltroOpen(false)}
          onApply={(data) => setDataFiltro(data)}
          onClear={() => setDataFiltro(null)}
          datasComSessao={datasComSessao}
          anchorRef={anchorRef}
        />
      )}
    </div>
  )
}
