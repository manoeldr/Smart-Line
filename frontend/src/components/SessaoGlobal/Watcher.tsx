import { useState, useEffect, useRef, useCallback } from 'react'
import { sessaoService } from '../../services/sessaoService'
import PrevisaoTerminoModal from '../../modals/PrevisaoTerminoModal'

const STORAGE_KEY = 'sessaoAtiva'
const ESTADO_KEY = 'estadoMedicao'

interface SessaoAtivaStorage {
  sessao: {
    id: string
    previsaoTermino: string | null
    status: string
  }
}

export default function Watcher() {
  const [modalOpen, setModalOpen] = useState(false)
  const [sessaoId, setSessaoId] = useState<string | null>(null)
  const alertaExibidoRef = useRef<string | null>(null)

  const verificar = useCallback(() => {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return

    try {
      const salvo = JSON.parse(raw) as SessaoAtivaStorage
      const previsao = salvo.sessao?.previsaoTermino
      const id = salvo.sessao?.id

      if (!previsao || !id) return

      const previsaoDate = new Date(previsao)
      const agora = new Date()

      if (agora >= previsaoDate && alertaExibidoRef.current !== id) {
        alertaExibidoRef.current = id
        setSessaoId(id)
        setModalOpen(true)
      }
    } catch {
      // ignora
    }
  }, [])

  useEffect(() => {
    verificar()
    const id = setInterval(verificar, 1000)
    return () => clearInterval(id)
  }, [verificar])

  async function handleEstender(novaHora: string) {
    if (!sessaoId) return
    try {
      const hoje = new Date()
      const [hh, mm] = novaHora.split(':').map(Number)
      const dt = new Date(hoje.getFullYear(), hoje.getMonth(), hoje.getDate(), hh, mm, 0)
      if (dt <= new Date()) dt.setDate(dt.getDate() + 1)

      const sessaoAtualizada = await sessaoService.estender(sessaoId, dt.toISOString())

      const raw = localStorage.getItem(STORAGE_KEY)
      if (raw) {
        const salvo = JSON.parse(raw)
        salvo.sessao = sessaoAtualizada
        localStorage.setItem(STORAGE_KEY, JSON.stringify(salvo))
      }

      alertaExibidoRef.current = null
      setModalOpen(false)
      setSessaoId(null)
    } catch {
      console.error('Erro ao estender medição')
    }
  }

  async function handleFinalizar() {
    if (!sessaoId) return
    try {
      await sessaoService.fechar(sessaoId)
    } catch {
      console.error('Erro ao finalizar medição')
    } finally {
      localStorage.removeItem(STORAGE_KEY)
      localStorage.removeItem(ESTADO_KEY)
      setModalOpen(false)
      setSessaoId(null)
      window.location.reload()
    }
  }

  return (
    <PrevisaoTerminoModal
      open={modalOpen}
      onEstender={handleEstender}
      onFinalizar={handleFinalizar}
    />
  )
}