import { useState, useEffect, useRef, useMemo } from 'react'
import type { Linha, MaquinaLinha } from '../../types'
import type { SessaoDto } from '../../services/sessaoService'
import { sessaoService } from '../../services/sessaoService'

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
}

type StatusMaquina = 'Rodando' | 'Parada'

export default function TelaMedicao({ maquina, linha, sessao, leiturasIniciais, onFinalizar }: Props) {
  const [status, setStatus] = useState<StatusMaquina>('Rodando')
  const [segundos, setSegundos] = useState(0)
  const [segundosParada, setSegundosParada] = useState(0)
  const [finalizando, setFinalizando] = useState(false)
  const paradaAtivaRef = useRef(false)

  const horaInicio = useMemo(() => new Date(sessao.inicio), [sessao.inicio])
  const horaInicioStr = useMemo(() =>
    horaInicio.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }),
    [horaInicio]
  )

  const [leituras, setLeituras] = useState<LeituraHoraria[]>([
    { hora: horaInicioStr, valor: String(leiturasIniciais['producao'] ?? ''), inicial: true }
  ])

  const ultimaHoraAdicionada = useRef(0)

  // Tick geral — atualiza cronômetro principal e de parada
  useEffect(() => {
    const inicio = horaInicio.getTime()
    const id = setInterval(() => {
      setSegundos(Math.floor((Date.now() - inicio) / 1000))
      if (paradaAtivaRef.current) {
        setSegundosParada(s => s + 1)
      }
    }, 1000)
    return () => clearInterval(id)
  }, [horaInicio])

  // Adiciona leitura horária a cada 1h
  useEffect(() => {
    const hora = Math.floor(segundos / 3600)
    if (hora === 0 || hora === ultimaHoraAdicionada.current) return
    ultimaHoraAdicionada.current = hora
    const novaHora = new Date(horaInicio.getTime() + hora * 3600000)
    const horaStr = novaHora.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
    setLeituras(prev => [...prev, { hora: horaStr, valor: '' }])
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

  async function handleFinalizar() {
    if (!confirm('Deseja finalizar a medição?')) return
    setFinalizando(true)
    try {
      await sessaoService.fechar(sessao.id)
      onFinalizar()
    } catch {
      alert('Erro ao finalizar medição.')
      setFinalizando(false)
    }
  }

  const ultimaLeitura = leituras[leituras.length - 1]

  return (
    <div className="max-w-lg mx-auto mt-4 flex flex-col gap-3">

      {/* Cabeçalho */}
      <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 rounded-md px-4 py-3 flex items-center justify-between">
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

      {/* Controles */}
      <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 rounded-md p-4">

        {/* Cronômetro */}
        <div className="text-center mb-4">
          <p className="text-4xl font-medium text-zinc-900 dark:text-zinc-100 tabular-nums tracking-widest">
            {formatarTempo(segundos)}
          </p>
          <p className="text-xs text-zinc-400 mt-1">tempo de medição</p>
          <div className={`inline-flex items-center gap-1.5 text-xs mt-2 px-3 py-1 rounded-full ${
            status === 'Rodando'
              ? 'bg-green-50 dark:bg-green-950 text-green-700 dark:text-green-400'
              : 'bg-red-50 dark:bg-red-950 text-red-700 dark:text-red-400'
          }`}>
            <span className={`w-1.5 h-1.5 rounded-full ${status === 'Rodando' ? 'bg-green-600' : 'bg-red-500'}`} />
            {status}
          </div>
        </div>

        {/* Indicadores */}
        <div className="grid grid-cols-3 gap-2 mb-4">
          <div className="bg-zinc-50 dark:bg-zinc-800 rounded-md p-2 text-center">
            <p className="text-[10px] text-zinc-400 mb-1">Tempo Medição</p>
            <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">{formatarTempo(segundos)}</p>
          </div>
          <div className="bg-zinc-50 dark:bg-zinc-800 rounded-md p-2 text-center">
            <p className="text-[10px] text-zinc-400 mb-1">Tempo Total Parado</p>
            <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">—</p>
          </div>
          <div className="bg-zinc-50 dark:bg-zinc-800 rounded-md p-2 text-center">
            <p className="text-[10px] text-zinc-400 mb-1">Tempo Parada Atual</p>
            <p className={`text-sm font-medium ${status === 'Parada' ? 'text-red-600 dark:text-red-400' : 'text-zinc-900 dark:text-zinc-100'}`}>
              {status === 'Parada' ? formatarTempo(segundosParada) : '—'}
            </p>
          </div>
        </div>

        {/* Marcha / Parada */}
        <div className="grid grid-cols-2 gap-2 mb-3">
          <button
            onClick={() => {
              setStatus('Rodando')
              paradaAtivaRef.current = false
              setSegundosParada(0)
            }}
            className={`h-11 flex items-center justify-center gap-2 rounded-md text-sm font-medium transition-colors ${
              status === 'Rodando'
                ? 'bg-green-600 text-white border border-green-600'
                : 'bg-green-50 dark:bg-green-950 text-green-700 dark:text-green-400 border border-green-200 dark:border-green-800 hover:bg-green-100'
            }`}
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><polygon points="5 3 19 12 5 21 5 3"/></svg>
            Marcha
          </button>
          <button
            onClick={() => {
              setStatus('Parada')
              paradaAtivaRef.current = true
              setSegundosParada(0)
            }}
            className={`h-11 flex items-center justify-center gap-2 rounded-md text-sm font-medium transition-colors ${
              status === 'Parada'
                ? 'bg-red-500 text-white border border-red-500'
                : 'bg-red-50 dark:bg-red-950 text-red-700 dark:text-red-400 border border-red-200 dark:border-red-800 hover:bg-red-100'
            }`}
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><rect x="6" y="4" width="4" height="16"/><rect x="14" y="4" width="4" height="16"/></svg>
            Parada
          </button>
        </div>

        {/* Foto strip */}
        {status === 'Parada' && (
          <div className="flex items-center gap-2 bg-zinc-50 dark:bg-zinc-800 rounded-md px-3 py-2 mb-3 text-xs text-zinc-500">
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z"/><circle cx="12" cy="13" r="4"/>
            </svg>
            Registrar foto da parada?
            <button className="ml-auto text-xs border border-zinc-200 dark:border-zinc-700 px-2 py-1 rounded hover:bg-zinc-100 dark:hover:bg-zinc-700">
              Tirar foto
            </button>
          </div>
        )}

        {/* Pausar / Finalizar */}
        <div className="grid grid-cols-2 gap-2">
          <button className="h-9 flex items-center justify-center gap-1.5 rounded-md text-xs text-zinc-500 border border-zinc-200 dark:border-zinc-700 hover:bg-zinc-50 dark:hover:bg-zinc-800">
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <circle cx="12" cy="12" r="10"/><line x1="10" y1="15" x2="10" y2="9"/><line x1="14" y1="15" x2="14" y2="9"/>
            </svg>
            Pausar medição
          </button>
          <button
            onClick={handleFinalizar}
            disabled={finalizando}
            className="h-9 flex items-center justify-center gap-1.5 rounded-md text-xs text-red-600 dark:text-red-400 border border-red-200 dark:border-red-800 hover:bg-red-50 dark:hover:bg-red-950 disabled:opacity-50"
          >
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1z"/><line x1="4" y1="22" x2="4" y2="15"/>
            </svg>
            {finalizando ? 'Finalizando...' : 'Finalizar medição'}
          </button>
        </div>
      </div>

      {/* Leituras */}
      <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 rounded-md p-4">
        <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100 mb-3 flex items-center gap-2">
          <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="#1961c0" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"/>
          </svg>
          Produção
        </p>

        <div className="flex items-center gap-3 pb-1.5 border-b border-zinc-200 dark:border-zinc-700 mb-1">
          <span className="text-[10px] font-medium text-zinc-400 min-w-[40px] flex-shrink-0">Horário</span>
          <span className="text-[10px] font-medium text-zinc-400 flex-1">Produção</span>
        </div>

        <div className="flex flex-col">
          {leituras.map((l, i) => (
            <div
              key={l.hora}
              className={`flex items-center gap-3 py-1.5 ${i < leituras.length - 1 ? 'border-b border-zinc-100 dark:border-zinc-800' : ''}`}
            >
              <span className={`text-xs min-w-[40px] flex-shrink-0 ${l.inicial ? 'text-blue-600 dark:text-blue-400 font-medium' : 'text-zinc-400'}`}>
                {l.hora}
              </span>
              <input
                type="number"
                min="0"
                value={l.valor}
                onChange={e => handleLeitura(l.hora, e.target.value)}
                disabled={l.inicial}
                placeholder={l === ultimaLeitura && !l.inicial ? 'agora' : '—'}
                className={`flex-1 h-7 px-2 rounded border text-xs focus:outline-none focus:ring-1 focus:ring-blue-500 ${
                  l.inicial
                    ? 'bg-blue-50 dark:bg-blue-950 border-blue-200 dark:border-blue-800 text-blue-700 dark:text-blue-300 cursor-not-allowed'
                    : l === ultimaLeitura
                      ? 'bg-zinc-50 dark:bg-zinc-800 border-green-300 dark:border-green-700 text-zinc-900 dark:text-zinc-100'
                      : 'bg-white dark:bg-zinc-900 border-zinc-200 dark:border-zinc-700 text-zinc-900 dark:text-zinc-100'
                }`}
              />
            </div>
          ))}
        </div>
      </div>

    </div>
  )
}