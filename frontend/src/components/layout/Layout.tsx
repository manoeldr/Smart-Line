import { Outlet } from 'react-router-dom'
import { useState, useRef, useEffect, type RefObject } from 'react'
import Sidebar from './Sidebar'
import Topbar from './Topbar'
import DateFilterModal from '../../modals/DateFilterModal'
import ClienteSelectorModal from '../../modals/ClienteSelectorModal'
import { clienteService } from '../../services/clienteService'
import { useAuth } from '../../contexts/AuthContext'
import type { Cliente } from '../../types'

export default function Layout() {
  const { usuario, clienteId, setClienteId } = useAuth()
  const [dataFiltro, setDataFiltro] = useState<string | null>(null)
  const [filtroOpen, setFiltroOpen] = useState(false)
  const [seletorOpen, setSeletorOpen] = useState(false)
  const [clientes, setClientes] = useState<Cliente[]>([])
  const [clienteAtual, setClienteAtual] = useState<Cliente | null>(null)
  const [filtroAnchor, setFiltroAnchor] = useState<RefObject<HTMLButtonElement | null> | null>(null)
  const seletorAnchorRef = useRef<HTMLButtonElement>(null)
  const datasComSessao: string[] = []

  useEffect(() => {
    if (usuario?.nivel === 'SuperAdmin') {
      clienteService.getAll().then(data => {
        setClientes(data)
        const atual = data.find(c => c.id === clienteId) ?? data[0] ?? null
        if (atual) {
          setClienteAtual(atual)
          if (!clienteId) setClienteId(atual.id)
        }
      })
    }
  }, [usuario, clienteId, setClienteId])

  function handleOpenFiltro(ref: RefObject<HTMLButtonElement | null>) {
    setFiltroAnchor(ref)
    setFiltroOpen(prev => !prev)
  }

  function handleSelectCliente(cliente: Cliente) {
    setClienteAtual(cliente)
    setClienteId(cliente.id)
    setDataFiltro(null)
  }

  return (
    <div className="flex h-screen bg-zinc-100 dark:bg-zinc-950">
      <Sidebar />
      <main className="flex-1 flex flex-col overflow-hidden">
        <Topbar
          titulo="Overview"
          dataFiltro={dataFiltro}
          clienteNome={clienteAtual?.nome ?? null}
          isSuperAdmin={usuario?.nivel === 'SuperAdmin'}
          seletorAnchorRef={seletorAnchorRef}
          onOpenFiltro={handleOpenFiltro}
          onOpenSeletor={() => setSeletorOpen(prev => !prev)}
        />
        <div className="flex-1 overflow-y-auto">
          <Outlet context={{ dataFiltro, setDataFiltro, filtroOpen, setFiltroOpen }} />
        </div>
      </main>

      {filtroAnchor && (
        <DateFilterModal
          open={filtroOpen}
          onClose={() => setFiltroOpen(false)}
          onApply={(data) => setDataFiltro(data)}
          onClear={() => setDataFiltro(null)}
          datasComSessao={datasComSessao}
          anchorRef={filtroAnchor}
        />
      )}

      <ClienteSelectorModal
        open={seletorOpen}
        onClose={() => setSeletorOpen(false)}
        onSelect={handleSelectCliente}
        clientes={clientes}
        anchorRef={seletorAnchorRef}
      />
    </div>
  )
}