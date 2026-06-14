import { useState, useEffect } from 'react'
import type { Linha, MaquinaLinha } from '../../types'
import { sessaoService, type SessaoDto } from '../../services/sessaoService'
import { linhaService } from '../../services/linhaService'
import { useAuth } from '../../contexts/AuthContext'
import SelecaoMaquina from './SelecaoMaquina'
import TelaMedicao from './TelaMedicao'

interface SessaoAtiva {
  maquina: MaquinaLinha
  linha: Linha
  sessao: SessaoDto
  leiturasIniciais: Record<string, number>
}

const STORAGE_KEY = 'sessaoAtiva'

export default function Medicao() {
  const { clienteId } = useAuth()
  const [sessaoAtiva, setSessaoAtiva] = useState<SessaoAtiva | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [restaurando, setRestaurando] = useState(true)

  // Restaura sessão ativa do localStorage ao montar
  useEffect(() => {
    async function restaurar() {
      const raw = localStorage.getItem(STORAGE_KEY)
      if (!raw || !clienteId) { setRestaurando(false); return }
      try {
        const salvo = JSON.parse(raw) as SessaoAtiva
        // Verifica se a sessão ainda está ativa na API
        const sessao = await sessaoService.getById(salvo.sessao.id)
        if (sessao && sessao.status === 'EmAndamento') {
          // Rebusca os dados atualizados da linha/máquina
          const linhas = await linhaService.getLinhasByCliente(clienteId)
          const linha = linhas.find(l => l.id === salvo.linha.id)
          const maquina = linha?.maquinas.find(m => m.id === salvo.maquina.id)
          if (linha && maquina) {
            setSessaoAtiva({ ...salvo, sessao, linha, maquina })
          } else {
            localStorage.removeItem(STORAGE_KEY)
          }
        } else {
          localStorage.removeItem(STORAGE_KEY)
        }
      } catch {
        localStorage.removeItem(STORAGE_KEY)
      } finally {
        setRestaurando(false)
      }
    }
    restaurar()
  }, [clienteId])

  async function handleIniciar(
    maquina: MaquinaLinha,
    linha: Linha,
    leiturasIniciais: Record<string, number>
  ) {
    setErro(null)
    setLoading(true)
    try {
      const sessao = await sessaoService.abrir(maquina.id)
      const nova: SessaoAtiva = { maquina, linha, sessao, leiturasIniciais }
      localStorage.setItem(STORAGE_KEY, JSON.stringify(nova))
      setSessaoAtiva(nova)
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao iniciar medição')
    } finally {
      setLoading(false)
    }
  }

  function handleFinalizar() {
    localStorage.removeItem(STORAGE_KEY)
    setSessaoAtiva(null)
  }

  if (restaurando) {
    return (
      <div className="flex items-center justify-center h-48 text-sm text-zinc-400">
        Carregando...
      </div>
    )
  }

  if (sessaoAtiva) {
    return (
      <TelaMedicao
        maquina={sessaoAtiva.maquina}
        linha={sessaoAtiva.linha}
        sessao={sessaoAtiva.sessao}
        leiturasIniciais={sessaoAtiva.leiturasIniciais}
        onFinalizar={handleFinalizar}
      />
    )
  }

  return (
    <>
      {erro && (
        <div className="max-w-lg mx-auto mt-4 px-4">
          <div className="bg-red-50 dark:bg-red-950 border border-red-200 dark:border-red-800 rounded-md px-3 py-2 text-xs text-red-600 dark:text-red-400">
            {erro}
          </div>
        </div>
      )}
      <SelecaoMaquina onIniciar={handleIniciar} loading={loading} />
    </>
  )
}