import { Outlet } from 'react-router-dom'
import { useState, useRef, useEffect, type RefObject } from 'react'
import Sidebar from './Sidebar'
import Topbar from './Topbar'
import DateFilterModal from '../../modals/DateFilterModal'
import ClienteSelectorModal from '../../modals/ClienteSelectorModal'
import Watcher from '../SessaoGlobal/Watcher'
import { clienteService } from '../../services/clienteService'
import { linhaService } from '../../services/linhaService'
import { useAuth } from '../../contexts/AuthContext'
import type { Cliente, Linha } from '../../types'
import { useManterRolagem } from '../../hooks/useManterRolagem'

export default function Layout() {
  const { usuario, clienteId, setClienteId, linhaId, setLinhaId } = useAuth()
  const superAdmin = usuario?.nivel === 'Administrador' || usuario?.nivel === 'Desenvolvedor'
  // Linhas do cliente atual, para o seletor do topo
  const [linhas, setLinhas] = useState<Linha[]>([])
  const [dataFiltro, setDataFiltro] = useState<string | null>(null)
  const [filtroOpen, setFiltroOpen] = useState(false)
  const [seletorOpen, setSeletorOpen] = useState(false)
  const [clientes, setClientes] = useState<Cliente[]>([])
  const [clienteAtual, setClienteAtual] = useState<Cliente | null>(null)
  const [filtroAnchor, setFiltroAnchor] = useState<RefObject<HTMLButtonElement | null> | null>(null)
  const seletorAnchorRef = useRef<HTMLButtonElement>(null)
  // Área que rola: o F5 volta na mesma posição
  const areaRolagemRef = useRef<HTMLDivElement>(null)
  useManterRolagem(areaRolagemRef)
  const [datasComSessao, setDatasComSessao] = useState<string[]>([])

  useEffect(() => {
    if (superAdmin) {
      clienteService.getAll().then(data => {
        setClientes(data)
        const atual = data.find(c => c.id === clienteId) ?? data[0] ?? null
        if (atual) {
          setClienteAtual(atual)
          if (!clienteId) setClienteId(atual.id)
        }
      })
    } else if (clienteId) {
      // Cliente fixo no cadastro: só o nome, para o topo
      clienteService.getById(clienteId).then(setClienteAtual).catch(() => setClienteAtual(null))
    }
  }, [superAdmin, clienteId, setClienteId])

  // Linhas do cliente escolhido; sem linha válida escolhida, fica a primeira
  useEffect(() => {
    if (!clienteId) return
    let ativo = true
    linhaService.getLinhasByCliente(clienteId).then(ls => {
      if (!ativo) return
      setLinhas(ls)
      if (!ls.some(l => l.id === linhaId)) setLinhaId(ls[0]?.id ?? null)
    }).catch(() => { if (ativo) setLinhas([]) })
    return () => { ativo = false }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clienteId])
  const linhaAtual = linhas.find(l => l.id === linhaId) ?? null

  function handleOpenFiltro(ref: RefObject<HTMLButtonElement | null>) {
    setFiltroAnchor(ref)
    setFiltroOpen(prev => !prev)
    // Dias com sessão (bolinha azul no calendário): busca na hora de abrir, sempre atualizado.
    if (!filtroOpen && clienteId) {
      clienteService.getDatasComSessao(clienteId)
        .then(setDatasComSessao)
        .catch(() => setDatasComSessao([]))
    }
  }

  function handleSelectCliente(cliente: Cliente) {
    setClienteAtual(cliente)
    setClienteId(cliente.id)
    setDataFiltro(null)
  }

  return (
    <div className="flex h-screen bg-fundo">
      <Sidebar />
      <main className="flex-1 flex flex-col overflow-hidden">
        <Topbar
          titulo="Overview"
          dataFiltro={dataFiltro}
          clienteNome={clienteAtual?.nome ?? null}
          linhaNome={linhaAtual?.nome ?? null}
          isSuperAdmin={superAdmin}
          seletorAnchorRef={seletorAnchorRef}
          onOpenFiltro={handleOpenFiltro}
          onOpenSeletor={() => setSeletorOpen(prev => !prev)}
        />
        <div ref={areaRolagemRef} className="flex-1 overflow-y-auto">
          <Outlet context={{ dataFiltro, setDataFiltro, filtroOpen, setFiltroOpen }} />
        </div>
      </main>

      {filtroAnchor && filtroOpen && (
        <DateFilterModal
          open={filtroOpen}
          valor={dataFiltro}
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
        podeTrocarCliente={superAdmin}
        linhas={linhas}
        linhaId={linhaId}
        onSelectLinha={setLinhaId}
        clienteAtualId={clienteId}
        anchorRef={seletorAnchorRef}
      />

      <Watcher />
    </div>
  )
}