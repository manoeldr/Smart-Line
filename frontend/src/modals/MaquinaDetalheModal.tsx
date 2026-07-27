import { useEffect, useState } from 'react'
import {
  ComposedChart, Bar, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer
} from 'recharts'
import { sessaoDetalheService, type SessaoDetalheDto } from '../services/sessaoDetalheService'

interface Props {
  open: boolean
  maquinaLinhaId: string | null
  onFechar: () => void
}

function formatarHoras(ms: number) {
  const horas = Math.floor(ms / 3600000)
  const minutos = Math.floor((ms % 3600000) / 60000)
  return `${horas}h ${minutos}m`
}

function formatarHora(dataIso: string) {
  return new Date(dataIso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
}

function formatarDataHora(dataIso: string) {
  return new Date(dataIso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })
}

const CORES_LINHA = ['#3b82f6', '#f59e0b', '#10b981', '#ef4444', '#8b5cf6', '#ec4899']

export default function MaquinaDetalheModal({ open, maquinaLinhaId, onFechar }: Props) {
  const [dados, setDados] = useState<SessaoDetalheDto | null>(null)
  const [loading, setLoading] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [camposSelecionados, setCamposSelecionados] = useState<Set<string>>(new Set())

  useEffect(() => {
    if (!open || !maquinaLinhaId) return
    async function carregar() {
      setLoading(true)
      setErro(null)
      setDados(null)
      try {
        const data = await sessaoDetalheService.getUltimaSessaoDetalhe(maquinaLinhaId!)
        setDados(data)
        setCamposSelecionados(new Set())
      } catch (e: unknown) {
        const mensagem = e instanceof Error ? e.message : ''
        if (mensagem.includes('404')) {
          setErro(null)
        } else {
          setErro(mensagem || 'Erro ao carregar dados')
        }
      } finally {
        setLoading(false)
      }
    }
    carregar()
  }, [open, maquinaLinhaId])

  function toggleCampo(id: string) {
    setCamposSelecionados(prev => {
      const novo = new Set(prev)
      if (novo.has(id)) novo.delete(id)
      else novo.add(id)
      return novo
    })
  }

  if (!open) return null

  const dadosGrafico = (() => {
    if (!dados) return []
    const horarios = new Set<string>()
    dados.pontosProducao.forEach(p => horarios.add(p.hora))
    dados.camposExtras.forEach(c => c.pontos.forEach(p => horarios.add(p.hora)))

    const horariosOrdenados = Array.from(horarios).sort()

    return horariosOrdenados.map(hora => {
      const ponto: Record<string, string | number> = { hora: formatarHora(hora) }
      const producaoPonto = dados.pontosProducao.find(p => p.hora === hora)
      if (producaoPonto) ponto['Produção'] = producaoPonto.quantidade

      dados.camposExtras.forEach(campo => {
        const extraPonto = campo.pontos.find(p => p.hora === hora)
        if (extraPonto) ponto[campo.nome] = extraPonto.valor
      })

      return ponto
    })
  })()

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 w-[900px] max-h-[90vh] flex flex-col">

        {/* Header */}
        <div className="px-5 py-4 border-b border-zinc-200 dark:border-zinc-800 flex items-center justify-between">
          <div>
            <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">
              {dados?.maquinaNome ?? 'Detalhes da máquina'}
            </p>
            {dados && (
              <p className="text-xs text-zinc-400 mt-0.5">
                {formatarDataHora(dados.inicio)} {dados.fim ? `— ${formatarDataHora(dados.fim)}` : '— em andamento'}
                {dados.status === 'EmAndamento' && (
                  <span className="ml-2 text-[10px] px-1.5 py-0.5 bg-green-50 dark:bg-green-950 text-green-700 dark:text-green-400">ativa</span>
                )}
              </p>
            )}
          </div>
          <button onClick={onFechar} className="text-zinc-400 hover:text-zinc-900 dark:hover:text-zinc-100">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-5 py-4">
          {loading ? (
            <p className="text-xs text-zinc-400 text-center py-8">Carregando...</p>
          ) : erro ? (
            <p className="text-xs text-red-400 text-center py-8">{erro}</p>
          ) : !dados ? (
            <p className="text-xs text-zinc-400 text-center py-8">Nenhuma sessão registrada para esta máquina</p>
          ) : (
            <>
              {/* Grid de métricas */}
              <div className="grid grid-cols-5 gap-2 mb-6">
                <MetricaCard label="OEE" valor={`${dados.oee}%`} destaque />
                <MetricaCard label="Eficiência" valor={`${dados.eficiencia}%`} />
                <MetricaCard label="Disponibilidade" valor={`${dados.disponibilidade}%`} />
                <MetricaCard label="Qualidade" valor={`${dados.qualidade}%`} />
                <MetricaCard label="Tempo Rodando" valor={formatarHoras(dados.tempoRodandoMs)} />
                <MetricaCard label="Tempo Parado" valor={formatarHoras(dados.tempoParadoMs)} />
                <MetricaCard label="Produção Total" valor={dados.producao.toLocaleString('pt-BR')} />
                <MetricaCard label="Refugo Total" valor={dados.refugo.toLocaleString('pt-BR')} />
                <MetricaCard label="MTTR" valor={dados.mttrMs !== null ? formatarHoras(dados.mttrMs) : '—'} />
                <MetricaCard label="MTBF" valor={dados.mtbfMs !== null ? formatarHoras(dados.mtbfMs) : '—'} />
              </div>

              {/* Seletor de campos do gráfico */}
              <div className="mb-3">
                <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100 mb-2">Gráfico por hora</p>
                <div className="flex flex-wrap gap-2">
                  <span className="text-[10px] px-2 py-1 bg-blue-50 dark:bg-blue-950 text-blue-600 dark:text-blue-400">Produção (sempre visível)</span>
                  {dados.camposExtras.map((campo, i) => (
                    <button
                      key={campo.campoMaquinaId}
                      onClick={() => toggleCampo(campo.campoMaquinaId)}
                      className={`text-[10px] px-2 py-1 border transition-colors ${
                        camposSelecionados.has(campo.campoMaquinaId)
                          ? 'text-white border-transparent'
                          : 'border-zinc-200 dark:border-zinc-700 text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800'
                      }`}
                      style={camposSelecionados.has(campo.campoMaquinaId) ? { backgroundColor: CORES_LINHA[i % CORES_LINHA.length] } : {}}
                    >
                      {campo.nome}{campo.unidade ? ` (${campo.unidade})` : ''}
                    </button>
                  ))}
                </div>
              </div>

              {/* Gráfico */}
              <div className="h-64 mb-6">
                <ResponsiveContainer width="100%" height="100%">
                  <ComposedChart data={dadosGrafico}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#e4e4e7" />
                    <XAxis dataKey="hora" tick={{ fontSize: 10 }} />
                    <YAxis yAxisId="left" tick={{ fontSize: 10 }} />
                    <YAxis yAxisId="right" orientation="right" tick={{ fontSize: 10 }} />
                    <Tooltip />
                    <Legend wrapperStyle={{ fontSize: 11 }} />
                    <Bar yAxisId="left" dataKey="Produção" fill="#1961c0" />
                    {dados.camposExtras
                      .filter(c => camposSelecionados.has(c.campoMaquinaId))
                      .map((campo, i) => (
                        <Line
                          key={campo.campoMaquinaId}
                          yAxisId="right"
                          type="monotone"
                          dataKey={campo.nome}
                          stroke={CORES_LINHA[i % CORES_LINHA.length]}
                          strokeWidth={2}
                        />
                      ))}
                  </ComposedChart>
                </ResponsiveContainer>
              </div>

              {/* Timeline de eventos */}
              <div>
                <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100 mb-2">Linha do tempo</p>
                <div className="flex flex-col">
                  {dados.eventos.map((evento, i) => (
                    <div key={i} className="flex items-start gap-3 py-2 border-b border-zinc-100 dark:border-zinc-800 last:border-0">
                      <div className={`w-2 h-2 rounded-full mt-1 flex-shrink-0 ${evento.tipo === 'Marcha' ? 'bg-green-600' : 'bg-red-500'}`} />
                      <div className="flex-1">
                        <div className="flex items-center gap-2">
                          <span className={`text-xs font-medium ${evento.tipo === 'Marcha' ? 'text-green-700 dark:text-green-400' : 'text-red-700 dark:text-red-400'}`}>
                            {evento.tipo}
                          </span>
                          <span className="text-[10px] text-zinc-400">{formatarDataHora(evento.horario)}</span>
                        </div>
                        {evento.motivoNome && (
                          <p className="text-[11px] text-zinc-500 mt-0.5">
                            {evento.motivoNome}
                            {evento.duracaoMs !== null && ` — ${formatarHoras(evento.duracaoMs!)}`}
                          </p>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  )
}

function MetricaCard({ label, valor, destaque }: { label: string; valor: string; destaque?: boolean }) {
  return (
    <div className="bg-zinc-50 dark:bg-zinc-800 p-2.5 text-center">
      <p className={`text-sm font-medium ${destaque ? 'text-blue-600 dark:text-blue-400' : 'text-zinc-900 dark:text-zinc-100'}`}>
        {valor}
      </p>
      <p className="text-[9px] text-zinc-400 mt-0.5">{label}</p>
    </div>
  )
}