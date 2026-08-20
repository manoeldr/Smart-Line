// Tela principal de medição — cronômetro, controles Marcha/Parada/Pausa,
// tabela de leituras horárias (produção + campos extras dinâmicos) e finalização.
// Máquinas com medeProducao=false não mostram a coluna de Produção — só os campos extras.
import { useState, useEffect, useRef, useMemo } from 'react'
import type { Linha, MaquinaLinha } from '../../types'
import type { SessaoDto } from '../../services/sessaoService'
import { sessaoService } from '../../services/sessaoService'
import { maquinaService, type MotivoParadaDto } from '../../services/maquinaService'
import { paradaService, type ParadaDto } from '../../services/paradaService'
import { producaoService } from '../../services/producaoService'
import { leituraExtraService } from '../../services/leituraExtraService'
import { configuracaoService, type CampoMaquinaDto } from '../../services/configuracaoService'
import MotivoParadaModal from '../../modals/MotivoParadaModal'
import PausarMedicaoModal from '../../modals/PausarMedicaoModal'
import LeituraFinalModal from '../../modals/LeituraFinalModal'
import { metricaBox, metricaValor, metricaLabel } from '../../styles/cards'
import { inputDisabled } from '../../styles/inputs'

interface Props {
  maquina: MaquinaLinha
  linha: Linha
  sessao: SessaoDto
  leiturasIniciais: Record<string, number>
  onFinalizar: () => void
}

interface LeituraHoraria {
  hora: string
  valor: string
  inicial?: boolean
  extras: Record<string, string> // campoId -> valor
}

type StatusMaquina = 'Rodando' | 'Parada' | 'Pausada'

const ESTADO_KEY = 'estadoMedicao'

interface EstadoSalvo {
  sessaoId: string
  status: StatusMaquina
  leituras: LeituraHoraria[]
  segundosTotalParado?: number
  paradaAtiva?: ParadaDto | null
  leiturasSalvas?: string[]
}

export default function TelaMedicao({ maquina, linha, sessao, leiturasIniciais, onFinalizar }: Props) {
  const horaInicio = useMemo(() => new Date(sessao.inicio), [sessao.inicio])
  const horaInicioStr = useMemo(() =>
    horaInicio.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }),
    [horaInicio]
  )

  const estadoSalvo = useMemo<EstadoSalvo | null>(() => {
    try {
      const raw = localStorage.getItem(ESTADO_KEY)
      if (!raw) return null
      const parsed = JSON.parse(raw) as EstadoSalvo
      return parsed.sessaoId === sessao.id ? parsed : null
    } catch { return null }
  }, [sessao.id])

  const [status, setStatus] = useState<StatusMaquina>(estadoSalvo?.status ?? 'Rodando')
  const [segundos, setSegundos] = useState(() =>
    Math.floor((Date.now() - new Date(sessao.inicio).getTime()) / 1000)
  )
  // Tempo da parada ATUAL — recalculado a partir do horário real de início (paradaAtiva.inicio),
  // não um contador simples, pra sobreviver a sair/voltar da tela.
  const [segundosParada, setSegundosParada] = useState(() => {
    if (estadoSalvo?.status === 'Parada' && estadoSalvo.paradaAtiva) {
      return Math.floor((Date.now() - new Date(estadoSalvo.paradaAtiva.inicio).getTime()) / 1000)
    }
    return 0
  })
  // Acumula o tempo de TODAS as paradas da sessão (não reseta a cada nova parada, diferente de segundosParada)
  const [segundosTotalParado, setSegundosTotalParado] = useState(estadoSalvo?.segundosTotalParado ?? 0)
  const [finalizando, setFinalizando] = useState(false)
  const paradaAtivaRef = useRef(false)
  const pausadaRef = useRef(false)
  // Trava síncrona contra clique duplo no botão Parada — mesmo problema que já corrigimos
  // no Salvar: `status` só reflete na tela depois do próximo render, então dois cliques bem
  // rápidos podiam abrir duas paradas ao mesmo tempo pra mesma sessão.
  const abrindoParadaRef = useRef(false)
  const [modalMotivoOpen, setModalMotivoOpen] = useState(false)
  const [modalPausaOpen, setModalPausaOpen] = useState(false)
  const [modalFinalOpen, setModalFinalOpen] = useState(false)
  const [motivos, setMotivos] = useState<MotivoParadaDto[]>([])
  const [loadingMotivos, setLoadingMotivos] = useState(false)
  const [loadingPausa, setLoadingPausa] = useState(false)
  const [paradaAtiva, setParadaAtiva] = useState<ParadaDto | null>(estadoSalvo?.paradaAtiva ?? null)
  const [enviandoFoto, setEnviandoFoto] = useState(false)
  const [fotoEnviada, setFotoEnviada] = useState(false)
  const [leiturasSalvas, setLeiturasSalvas] = useState<Set<string>>(new Set(estadoSalvo?.leiturasSalvas ?? []))

  // Campos extras selecionados nesta sessão
  const [camposExtras, setCamposExtras] = useState<CampoMaquinaDto[]>([])

  const [leituras, setLeituras] = useState<LeituraHoraria[]>(
    estadoSalvo?.leituras ?? [
      {
        hora: horaInicioStr,
        valor: String(leiturasIniciais['producao'] ?? ''),
        inicial: true,
        extras: Object.fromEntries(
          Object.entries(leiturasIniciais)
            .filter(([k]) => k !== 'producao')
            .map(([k, v]) => [k, String(v)])
        ),
      }
    ]
  )

  const ultimaHoraAdicionada = useRef(
    estadoSalvo?.leituras
      ? Math.max(0, estadoSalvo.leituras.filter(l => !l.inicial).length)
      : 0
  )

  // Horário real de início da parada atual (timestamp em ms) — usado pra recalcular
  // segundosParada a cada tick do cronômetro, em vez de um contador que zera ao remontar.
  const paradaInicioRef = useRef<number | null>(
    estadoSalvo?.status === 'Parada' && estadoSalvo.paradaAtiva
      ? new Date(estadoSalvo.paradaAtiva.inicio).getTime()
      : null
  )

  useEffect(() => {
    paradaInicioRef.current = paradaAtiva ? new Date(paradaAtiva.inicio).getTime() : null
  }, [paradaAtiva])

  // Busca detalhes dos campos extras selecionados na sessão (para montar as colunas dinâmicas)
  useEffect(() => {
    if (!sessao.camposSelecionados || sessao.camposSelecionados.length === 0) return
    async function carregar() {
      try {
        const todosCampos = await configuracaoService.getCamposMaquina(maquina.maquinaId)
        const selecionados = todosCampos.filter(c => sessao.camposSelecionados.includes(c.id))
        setCamposExtras(selecionados.sort((a, b) => a.ordem - b.ordem))
      } catch {
        console.error('Erro ao carregar campos extras')
      }
    }
    carregar()
  }, [sessao.camposSelecionados, maquina.maquinaId])

  // Persiste o estado da medição a cada mudança — permite retomar ao sair e voltar da tela
  useEffect(() => {
    const estado: EstadoSalvo = {
      sessaoId: sessao.id,
      status,
      leituras,
      segundosTotalParado,
      paradaAtiva,
      leiturasSalvas: Array.from(leiturasSalvas),
    }
    localStorage.setItem(ESTADO_KEY, JSON.stringify(estado))
  }, [sessao.id, status, leituras, segundosTotalParado, paradaAtiva, leiturasSalvas])

  useEffect(() => {
    paradaAtivaRef.current = status === 'Parada'
    pausadaRef.current = status === 'Pausada'
  }, [status])

  // Cronômetro — congela durante pausa planejada, soma tempo de parada quando aplicável
  useEffect(() => {
    const inicio = horaInicio.getTime()
    let pausaInicio = 0
    let totalPausadoMs = 0

    const id = setInterval(() => {
      if (pausadaRef.current) {
        if (pausaInicio === 0) pausaInicio = Date.now()
        return
      }
      if (pausaInicio > 0) {
        totalPausadoMs += Date.now() - pausaInicio
        pausaInicio = 0
      }
      setSegundos(Math.floor((Date.now() - inicio - totalPausadoMs) / 1000))
      if (paradaAtivaRef.current) {
        if (paradaInicioRef.current !== null) {
          setSegundosParada(Math.floor((Date.now() - paradaInicioRef.current) / 1000))
        }
        setSegundosTotalParado(s => s + 1)
      }
    }, 1000)
    return () => clearInterval(id)
  }, [horaInicio])

  // Adiciona uma nova linha de leitura a cada hora completa
  useEffect(() => {
    const hora = Math.floor(segundos / 3600)
    if (hora === 0 || hora === ultimaHoraAdicionada.current) return
    ultimaHoraAdicionada.current = hora
    const novaHora = new Date(horaInicio.getTime() + hora * 3600000)
    const horaStr = novaHora.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
    setLeituras(prev => {
      if (prev.some(l => l.hora === horaStr)) return prev
      return [...prev, { hora: horaStr, valor: '', extras: {} }]
    })
  }, [segundos, horaInicio])

  function formatarTempo(seg: number) {
    const h = String(Math.floor(seg / 3600)).padStart(2, '0')
    const m = String(Math.floor((seg % 3600) / 60)).padStart(2, '0')
    const s = String(seg % 60).padStart(2, '0')
    return `${h}:${m}:${s}`
  }

  function handleLeitura(hora: string, valor: string) {
    setLeituras(prev => prev.map(l => l.hora === hora ? { ...l, valor } : l))
  }

  function handleLeituraExtra(hora: string, campoId: string, valor: string) {
    setLeituras(prev => prev.map(l =>
      l.hora === hora ? { ...l, extras: { ...l.extras, [campoId]: valor } } : l
    ))
  }

  async function handleSalvarLeitura(hora: string, valor: string) {
    const leitura = leituras.find(l => l.hora === hora)
    const agora = new Date()

    try {
      // Só registra produção se a máquina de fato mede — caso contrário, "valor" fica
      // sempre vazio (não existe input pra ele na tela) e não deve virar uma chamada à API.
      if (maquina.medeProducao) {
        const quantidade = parseInt(valor)
        if (isNaN(quantidade) || quantidade < 0) return
        await producaoService.registrar(sessao.id, quantidade, 0, agora)
      }

      // Salva os campos extras preenchidos nesta mesma leitura
      if (leitura && camposExtras.length > 0) {
        const promessas = camposExtras
          .filter(c => leitura.extras[c.id] !== undefined && leitura.extras[c.id] !== '')
          .map(c => leituraExtraService.registrar(sessao.id, c.id, Number(leitura.extras[c.id]) || 0, agora))
        await Promise.all(promessas)
      }

      setLeiturasSalvas(prev => new Set([...prev, hora]))
    } catch {
      console.error('Erro ao salvar leitura')
    }
  }

  async function handleTirarFoto(e: React.ChangeEvent<HTMLInputElement>) {
    const arquivo = e.target.files?.[0]
    if (!arquivo || !paradaAtiva) return

    setEnviandoFoto(true)
    try {
      await paradaService.uploadFoto(paradaAtiva.id, arquivo)
      setFotoEnviada(true)
    } catch {
      console.error('Erro ao enviar foto')
      alert('Erro ao enviar a foto. Tente novamente.')
    } finally {
      setEnviandoFoto(false)
    }
  }

  // Só faz sentido abrir o seletor de motivo quando está DE FATO saindo de uma parada —
  // clicar em Marcha com a máquina já rodando não deve fazer nada.
  async function handleMarcha() {
    if (status !== 'Parada') return
    setStatus('Rodando')
    paradaAtivaRef.current = false
    setSegundosParada(0)
    paradaInicioRef.current = null
    setLoadingMotivos(true)
    setModalMotivoOpen(true)
    try {
      const data = await maquinaService.getMotivosParada(maquina.maquinaId)
      setMotivos(data)
    } finally {
      setLoadingMotivos(false)
    }
  }

  async function handlePausar() {
    setLoadingPausa(true)
    setModalPausaOpen(true)
    try {
      const data = await maquinaService.getMotivosParada(maquina.maquinaId)
      setMotivos(data)
    } finally {
      setLoadingPausa(false)
    }
  }

  function handleAbrirFinalizar() {
    setModalFinalOpen(true)
  }

  async function handleConfirmarFinalizar(
    producaoFinal: number,
    extras: { campoMaquinaId: string; valor: number }[]
  ) {
    setFinalizando(true)
    try {
      await sessaoService.finalizar(sessao.id, producaoFinal, 0, extras)
      localStorage.removeItem(ESTADO_KEY)
      setModalFinalOpen(false)
      onFinalizar()
    } catch {
      alert('Erro ao finalizar medição.')
      setFinalizando(false)
    }
  }

  const ultimaLeitura = leituras[leituras.length - 1]

  // Cores do badge de status — específicas dessa tela (Rodando/Parada/Pausada), não fazem
  // parte do sistema de estilos genérico porque são um conjunto de 3 estados só usado aqui
  const badgeColor = {
    Rodando: 'bg-green-50 dark:bg-green-950 text-green-700 dark:text-green-400',
    Parada:  'bg-red-50 dark:bg-red-950 text-red-700 dark:text-red-400',
    Pausada: 'bg-amber-50 dark:bg-amber-950 text-amber-700 dark:text-amber-400',
  }

  const dotColor = {
    Rodando: 'bg-green-600',
    Parada:  'bg-red-500',
    Pausada: 'bg-amber-500',
  }

  // Grid dinâmico: Horário | [Produção, só se medeProducao] | [campo extra]... | check
  const gridTemplate = `80px ${maquina.medeProducao ? '1fr ' : ''}${camposExtras.map(() => '1fr').join(' ')} 40px`.trim()

  return (
    <div className="flex flex-col h-full">

      {/* Cabeçalho */}
      <div className="bg-white dark:bg-zinc-900 border-b border-zinc-200 dark:border-zinc-800 px-6 py-3 flex items-center justify-between">
        <div>
          <p className="text-xs text-zinc-400">{linha.nome}</p>
          <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100 flex items-center gap-2">
            {maquina.maquinaNome}
            {maquina.critica && (
              <span className="text-[10px] text-blue-600 bg-blue-50 dark:bg-blue-950 dark:text-blue-400 px-1.5 py-0.5 rounded font-medium">crítica</span>
            )}
          </p>
        </div>
        <p className="text-xs text-zinc-400">Iniciado às {horaInicioStr}</p>
      </div>

      {/* Layout duas colunas */}
      <div className="flex flex-1 overflow-hidden">

        {/* Coluna esquerda — Controles */}
        <div className="w-1/2 flex-shrink-0 border-r border-zinc-200 dark:border-zinc-800 flex flex-col overflow-y-auto">

          {/* Cronômetro */}
          <div className="border-b border-zinc-200 dark:border-zinc-800 px-6 py-6 text-center">
            <p className="text-5xl font-medium text-zinc-900 dark:text-zinc-100 tabular-nums tracking-widest">
              {formatarTempo(segundos)}
            </p>
            <p className="text-xs text-zinc-400 mt-1">tempo de medição</p>
            <div className={`inline-flex items-center gap-1.5 text-xs mt-3 px-3 py-1 rounded-full ${badgeColor[status]}`}>
              <span className={`w-1.5 h-1.5 rounded-full ${dotColor[status]}`} />
              {status}
            </div>
          </div>

          {/* Indicadores */}
          <div className="grid grid-cols-3 border-b border-zinc-200 dark:border-zinc-800">
            <div className={`${metricaBox} border-r border-zinc-200 dark:border-zinc-800`}>
              <p className={metricaLabel}>Tempo Medição</p>
              <p className={metricaValor}>{formatarTempo(segundos)}</p>
            </div>
            <div className={`${metricaBox} border-r border-zinc-200 dark:border-zinc-800`}>
              <p className={metricaLabel}>Total Parado</p>
              <p className={metricaValor}>{formatarTempo(segundosTotalParado)}</p>
            </div>
            <div className={metricaBox}>
              <p className={metricaLabel}>Parada Atual</p>
              <p className={`text-sm font-medium ${status === 'Parada' ? 'text-red-600 dark:text-red-400' : 'text-zinc-900 dark:text-zinc-100'}`}>
                {status === 'Parada' ? formatarTempo(segundosParada) : '—'}
              </p>
            </div>
          </div>

          {/* Marcha / Parada */}
          <div className="grid grid-cols-2 border-b border-zinc-200 dark:border-zinc-800">
            <button
              onClick={handleMarcha}
              disabled={status === 'Pausada'}
              className={`h-14 flex items-center justify-center gap-2 text-sm font-medium transition-colors border-r border-zinc-200 dark:border-zinc-800 ${
                status === 'Rodando'
                  ? 'bg-green-600 text-white'
                  : status === 'Pausada'
                    ? 'opacity-40 cursor-not-allowed text-green-700 dark:text-green-400'
                    : 'text-green-700 dark:text-green-400 hover:bg-green-50 dark:hover:bg-green-950'
              }`}
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><polygon points="5 3 19 12 5 21 5 3"/></svg>
              Marcha
            </button>
            <button
              onClick={async () => {
                if (status === 'Parada' || abrindoParadaRef.current) return
                abrindoParadaRef.current = true
                setStatus('Parada')
                paradaAtivaRef.current = true
                setSegundosParada(0)
                setFotoEnviada(false)
                try {
                  const p = await paradaService.abrir(sessao.id, new Date())
                  setParadaAtiva(p)
                  paradaInicioRef.current = new Date(p.inicio).getTime()
                } catch {
                  console.error('Erro ao registrar parada')
                } finally {
                  abrindoParadaRef.current = false
                }
              }}
              disabled={status === 'Pausada'}
              className={`h-14 flex items-center justify-center gap-2 text-sm font-medium transition-colors ${
                status === 'Parada'
                  ? 'bg-red-500 text-white'
                  : status === 'Pausada'
                    ? 'opacity-40 cursor-not-allowed text-red-700 dark:text-red-400'
                    : 'text-red-700 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-950'
              }`}
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><rect x="6" y="4" width="4" height="16"/><rect x="14" y="4" width="4" height="16"/></svg>
              Parada
            </button>
          </div>

          {/* Banner pausa */}
          {status === 'Pausada' && (
            <div className="flex items-center justify-between bg-amber-50 dark:bg-amber-950 border-b border-amber-200 dark:border-amber-800 px-4 py-2.5 text-xs text-amber-700 dark:text-amber-400">
              <span>Medição pausada</span>
              <button
                onClick={() => {
                  setStatus('Rodando')
                  paradaAtivaRef.current = false
                }}
                className="text-[10px] border border-amber-300 dark:border-amber-700 px-2 py-1 hover:bg-amber-100 dark:hover:bg-amber-900"
              >
                Retomar
              </button>
            </div>
          )}

          {/* Foto strip */}
          {status === 'Parada' && paradaAtiva && (
            <div className="flex items-center gap-2 border-b border-zinc-200 dark:border-zinc-800 px-4 py-2.5 text-xs text-zinc-500">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z"/><circle cx="12" cy="13" r="4"/>
              </svg>
              {fotoEnviada ? 'Foto registrada' : 'Registrar foto da parada?'}
              {fotoEnviada ? (
                <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="#639922" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="ml-auto">
                  <polyline points="20 6 9 17 4 12"/>
                </svg>
              ) : (
                <label className="ml-auto text-xs border border-zinc-200 dark:border-zinc-700 px-2 py-1 hover:bg-zinc-100 dark:hover:bg-zinc-700 cursor-pointer">
                  {enviandoFoto ? 'Enviando...' : 'Tirar foto'}
                  <input
                    type="file"
                    accept="image/*"
                    capture="environment"
                    onChange={handleTirarFoto}
                    disabled={enviandoFoto}
                    className="hidden"
                  />
                </label>
              )}
            </div>
          )}

          {/* Pausar / Finalizar */}
          <div className="grid grid-cols-2 mt-auto">
            <button
              onClick={handlePausar}
              disabled={status === 'Pausada'}
              className="h-11 flex items-center justify-center gap-1.5 text-xs text-zinc-500 border-r border-t border-zinc-200 dark:border-zinc-800 hover:bg-zinc-50 dark:hover:bg-zinc-800 disabled:opacity-40 disabled:cursor-not-allowed"
            >
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="12" cy="12" r="10"/><line x1="10" y1="15" x2="10" y2="9"/><line x1="14" y1="15" x2="14" y2="9"/>
              </svg>
              Pausar medição
            </button>
            <button
              onClick={handleAbrirFinalizar}
              disabled={finalizando}
              className="h-11 flex items-center justify-center gap-1.5 text-xs text-red-600 dark:text-red-400 border-t border-zinc-200 dark:border-zinc-800 hover:bg-red-50 dark:hover:bg-red-950 disabled:opacity-50"
            >
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1z"/><line x1="4" y1="22" x2="4" y2="15"/>
              </svg>
              {finalizando ? 'Finalizando...' : 'Finalizar medição'}
            </button>
          </div>
        </div>

        {/* Coluna direita — Produção */}
        <div className="flex-1 flex flex-col overflow-hidden">

          {/* Header produção */}
          <div className="border-b border-zinc-200 dark:border-zinc-800 px-6 py-3 flex items-center gap-2">
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="#1961c0" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"/>
            </svg>
            <span className="text-xs font-medium text-zinc-900 dark:text-zinc-100">
              {maquina.medeProducao ? 'Produção' : 'Coleta'}
            </span>
          </div>

          {/* Header tabela */}
          <div
            className="grid border-b border-zinc-200 dark:border-zinc-800 px-6 py-2"
            style={{ gridTemplateColumns: gridTemplate }}
          >
            <span className="min-w-0 text-[10px] font-medium text-zinc-400">Horário</span>
            {maquina.medeProducao && (
              <span className="min-w-0 text-[10px] font-medium text-zinc-400 truncate">Produção</span>
            )}
            {camposExtras.map(c => (
              <span key={c.id} className="min-w-0 text-[10px] font-medium text-zinc-400 truncate">
                {c.nome}{c.unidade ? ` (${c.unidade})` : ''}
              </span>
            ))}
          </div>

          {/* Leituras */}
          <div className="flex-1 overflow-y-auto">
            {leituras.map((l, i) => (
              <div
                key={l.hora}
                className={`grid items-center px-6 py-2 gap-2 ${
                  i < leituras.length - 1 ? 'border-b border-zinc-100 dark:border-zinc-800' : ''
                }`}
                style={{ gridTemplateColumns: gridTemplate }}
              >
                <span className={`min-w-0 text-xs truncate ${l.inicial ? 'text-blue-600 dark:text-blue-400 font-medium' : 'text-zinc-400'}`}>
                  {l.hora}
                </span>

                {/* Produção — só aparece se a máquina mede produção */}
                {maquina.medeProducao && (
                  <input
                    type="number"
                    min="0"
                    value={l.valor}
                    onChange={e => handleLeitura(l.hora, e.target.value)}
                    disabled={l.inicial || leiturasSalvas.has(l.hora)}
                    placeholder={l === ultimaLeitura && !l.inicial ? 'agora' : '—'}
                    className={`min-w-0 w-full h-7 px-2 text-xs focus:outline-none focus:ring-1 focus:ring-blue-500 border ${
                      l.inicial || leiturasSalvas.has(l.hora)
                        ? inputDisabled
                        : l === ultimaLeitura
                          ? 'bg-zinc-50 dark:bg-zinc-800 border-green-300 dark:border-green-700 text-zinc-900 dark:text-zinc-100'
                          : 'bg-white dark:bg-zinc-900 border-zinc-200 dark:border-zinc-700 text-zinc-900 dark:text-zinc-100'
                    }`}
                  />
                )}

                {/* Campos extras */}
                {camposExtras.map(c => (
                  <input
                    key={c.id}
                    type="number"
                    value={l.extras?.[c.id] ?? ''}
                    onChange={e => handleLeituraExtra(l.hora, c.id, e.target.value)}
                    disabled={l.inicial || leiturasSalvas.has(l.hora)}
                    placeholder="—"
                    className={`min-w-0 w-full h-7 px-2 text-xs focus:outline-none focus:ring-1 focus:ring-blue-500 border ${
                      l.inicial || leiturasSalvas.has(l.hora)
                        ? 'bg-zinc-50 dark:bg-zinc-800 border-zinc-200 dark:border-zinc-700 text-zinc-400 cursor-not-allowed'
                        : 'bg-white dark:bg-zinc-900 border-zinc-200 dark:border-zinc-700 text-zinc-900 dark:text-zinc-100'
                    }`}
                  />
                ))}

                <div className="flex items-center justify-center">
                  {!l.inicial && !leiturasSalvas.has(l.hora) && (maquina.medeProducao ? l.valor : true) && (
                    <button
                      onClick={() => handleSalvarLeitura(l.hora, l.valor)}
                      className="h-7 w-7 flex items-center justify-center bg-blue-600 text-white hover:bg-blue-700 text-[10px]"
                    >
                      ✓
                    </button>
                  )}
                  {leiturasSalvas.has(l.hora) && (
                    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="#639922" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                      <polyline points="20 6 9 17 4 12"/>
                    </svg>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Modal motivo de parada */}
      <MotivoParadaModal
        open={modalMotivoOpen}
        motivos={motivos}
        loading={loadingMotivos}
        onCancelar={() => setModalMotivoOpen(false)}
        onConfirmar={async (motivoId) => {
          if (paradaAtiva) {
            try {
              await paradaService.fechar(paradaAtiva.id, motivoId, new Date())
            } catch {
              console.error('Erro ao fechar parada')
            }
            setParadaAtiva(null)
          }
          setModalMotivoOpen(false)
        }}
        onCadastrarNovo={async (nome, tipo) => {
          const novo = await maquinaService.criarMotivoParada(maquina.maquinaId, nome, tipo)
          setMotivos(prev => [...prev, novo])
          return novo.id
        }}
      />

      {/* Modal pausar medição */}
      <PausarMedicaoModal
        open={modalPausaOpen}
        motivos={motivos}
        loading={loadingPausa}
        onConfirmar={async (motivoId) => {
          setModalPausaOpen(false)
          setStatus('Pausada')
          paradaAtivaRef.current = false
          setSegundosParada(0)
          try {
            const p = await paradaService.abrir(sessao.id, new Date())
            setParadaAtiva(p)
            await paradaService.fechar(p.id, motivoId, new Date())
            setParadaAtiva(null)
          } catch {
            console.error('Erro ao registrar pausa')
          }
        }}
        onCancelar={() => setModalPausaOpen(false)}
        onCadastrarNovo={async (nome) => {
          const novo = await maquinaService.criarMotivoParadaPlanejado(maquina.maquinaId, nome)
          setMotivos(prev => [...prev, novo])
          return novo.id
        }}
      />

      {/* Modal leitura final */}
      <LeituraFinalModal
        open={modalFinalOpen}
        camposExtras={camposExtras}
        medeProducao={maquina.medeProducao}
        salvando={finalizando}
        onConfirmar={handleConfirmarFinalizar}
        onCancelar={() => setModalFinalOpen(false)}
      />
    </div>
  )
}