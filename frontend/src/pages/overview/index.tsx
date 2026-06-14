import { useEffect, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import { linhaService } from '../../services/linhaService'
import type { Linha } from '../../types'
import LinhaCard from './LinhaCard'

interface OutletContext {
  dataFiltro: string | null
  setDataFiltro: (d: string | null) => void
  filtroOpen: boolean
  setFiltroOpen: (v: boolean) => void
}

export default function Overview() {
  const { dataFiltro } = useOutletContext<OutletContext>()
  const { clienteId } = useAuth()
  const [linhas, setLinhas] = useState<Linha[]>([])
  const [loading, setLoading] = useState(true)
  const [erro, setErro] = useState<string | null>(null)

  useEffect(() => {
    if (!clienteId) return

    async function carregar() {
      setLoading(true)
      setErro(null)
      try {
        const data = await linhaService.getLinhasByCliente(clienteId!)
        setLinhas(data)
      } catch (e: unknown) {
        setErro(e instanceof Error ? e.message : 'Erro ao carregar linhas')
      } finally {
        setLoading(false)
      }
    }

    carregar()
    const id = setInterval(carregar, 30000)
    return () => clearInterval(id)
  }, [clienteId])

  if (!clienteId) {
    return (
      <div className="flex items-center justify-center h-48 text-sm text-zinc-400">
        Nenhum cliente selecionado
      </div>
    )
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center h-48 text-sm text-zinc-400">
        Carregando...
      </div>
    )
  }

  if (erro) {
    return (
      <div className="flex items-center justify-center h-48 text-sm text-red-400">
        Erro ao carregar linhas: {erro}
      </div>
    )
  }

  return (
    <div className="p-4 flex flex-col gap-3">
      {linhas.length === 0 ? (
        <div className="flex items-center justify-center h-48 text-sm text-zinc-400">
          Nenhuma linha cadastrada
        </div>
      ) : (
        linhas.map(linha => (
          <LinhaCard
            key={linha.id}
            linha={linha}
            filtroAtivo={dataFiltro !== null}
            dataFiltro={dataFiltro}
          />
        ))
      )}
    </div>
  )
}