// Aba "Logs" da Área do Desenvolvedor: as últimas linhas do log do backend (desde que ele
// subiu; some ao reiniciar). Pede ao backend só o que é novo a cada 2 s, filtra por nível e
// por texto, e acompanha o fim da lista enquanto o usuário não rolar para cima.
import { useEffect, useRef, useState } from 'react'
import { useParametroUrl } from '../../hooks/useParametroUrl'
import { desenvolvedorService, type LinhaLogDto, type NivelLog } from '../../services/desenvolvedorService'
import { mensagemErro } from '../../services/api'
import { btnSecondarySm } from '../../styles/buttons'
import { inputBase } from '../../styles/inputs'
import Switch from '../../components/Switch'
import { cardBase } from '../../styles/cards'

const INTERVALO_MS = 2000
const MAXIMO_NA_TELA = 2000

const NIVEIS: { id: NivelLog | ''; label: string }[] = [
  { id: '', label: 'Tudo' },
  { id: 'Warning', label: 'Avisos e erros' },
  { id: 'Error', label: 'Só erros' },
]

const COR_NIVEL: Record<string, string> = {
  Information: 'text-texto-3',
  Warning: 'text-amber-600 dark:text-amber-400',
  Error: 'text-red-600 dark:text-red-400',
  Critical: 'text-red-600 dark:text-red-400 font-semibold',
}

const SIGLA_NIVEL: Record<string, string> = {
  Information: 'INFO',
  Warning: 'AVISO',
  Error: 'ERRO',
  Critical: 'CRÍTICO',
}

function horaComSegundos(iso: string) {
  return new Date(iso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

export default function AbaLogs() {
  // Filtros na URL (?nivel=&texto=) para o F5 manter.
  const [nivelUrl, setNivelUrl] = useParametroUrl('nivel')
  const [textoUrl, setTextoUrl] = useParametroUrl('texto')
  const nivel = (NIVEIS.find(n => n.id === nivelUrl)?.id ?? '') as NivelLog | ''
  const texto = textoUrl ?? ''

  const [linhas, setLinhas] = useState<LinhaLogDto[]>([])
  const [ultima, setUltima] = useState(0)
  const [capacidade, setCapacidade] = useState(0)
  const [carregado, setCarregado] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [pausado, setPausado] = useState(false)
  const [seguir, setSeguir] = useState(true)
  // Exceções abertas (clicar na linha mostra o detalhe).
  const [abertas, setAbertas] = useState<Set<number>>(new Set())
  // "Limpar tela": esconde o que já foi visto; o backend continua guardando.
  const [limparDesde, setLimparDesde] = useState(0)

  const lista = useRef<HTMLDivElement>(null)

  // Mudou o filtro: recomeça do zero. Depois, só o que veio depois da última sequência.
  useEffect(() => {
    let ativo = true
    let depoisDe = 0
    let primeira = true
    async function carregar() {
      if (pausado && !primeira) return
      try {
        const r = await desenvolvedorService.logs(depoisDe, nivel, texto, MAXIMO_NA_TELA)
        if (!ativo) return
        setCapacidade(r.capacidade)
        setUltima(r.ultimaSequencia)
        if (primeira) {
          setLinhas(r.linhas)
        } else if (r.linhas.length > 0) {
          setLinhas(atual => [...atual, ...r.linhas].slice(-MAXIMO_NA_TELA))
        }
        // O backend devolve as linhas depois de "depoisDe", até 500 por vez: a próxima busca
        // continua de onde esta parou (ou da última sequência, se o filtro não trouxe nada).
        depoisDe = r.linhas.length > 0 ? r.linhas[r.linhas.length - 1].sequencia : r.ultimaSequencia
        primeira = false
        setErro(null)
      } catch (e) {
        if (ativo) setErro(mensagemErro(e, 'Não foi possível ler o log.'))
      } finally {
        if (ativo) setCarregado(true)
      }
    }
    carregar()
    const id = setInterval(carregar, INTERVALO_MS)
    return () => { ativo = false; clearInterval(id) }
  }, [nivel, texto, pausado])

  // Acompanha o fim enquanto "seguir" estiver marcado.
  const visiveis = linhas.filter(l => l.sequencia > limparDesde)
  const quantidade = visiveis.length
  useEffect(() => {
    const el = lista.current
    if (seguir && el) el.scrollTop = el.scrollHeight
  }, [quantidade, seguir])

  // Rolar para cima desliga o "seguir"; voltar ao fim religa.
  function aoRolar() {
    const el = lista.current
    if (!el) return
    const noFim = el.scrollHeight - el.scrollTop - el.clientHeight < 24
    if (noFim !== seguir) setSeguir(noFim)
  }

  function alternarExcecao(sequencia: number) {
    setAbertas(atual => {
      const novo = new Set(atual)
      if (novo.has(sequencia)) novo.delete(sequencia)
      else novo.add(sequencia)
      return novo
    })
  }

  return (
    <div className="flex flex-col h-full gap-3">
      {/* Filtros */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex">
          {NIVEIS.map(n => (
            <button
              key={n.id}
              onClick={() => setNivelUrl(n.id || null)}
              className={`h-8 px-3 text-xs border transition-colors ${
                nivel === n.id
                  ? 'bg-accent-600 text-accent-fg border-accent-600'
                  : 'border-borda-forte text-texto-2 hover:bg-superficie-2'
              }`}
            >
              {n.label}
            </button>
          ))}
        </div>
        <input
          value={texto}
          onChange={e => setTextoUrl(e.target.value || null)}
          placeholder="Procurar no texto (ex.: WISE, 192.168, parada)"
          className={`${inputBase} w-72`}
        />
        <div className="flex items-center gap-1.5 text-xs text-texto-2">
          <Switch checked={!pausado} onChange={v => setPausado(!v)} />
          <span onClick={() => setPausado(p => !p)} className="cursor-pointer">Atualizar sozinho</span>
        </div>
        <div className="flex items-center gap-1.5 text-xs text-texto-2">
          <Switch checked={seguir} onChange={setSeguir} />
          <span onClick={() => setSeguir(s => !s)} className="cursor-pointer">Seguir o fim</span>
        </div>
        <button onClick={() => setLimparDesde(ultima)} className={btnSecondarySm}>Limpar tela</button>
        <span className="text-xs text-texto-3 ml-auto">
          {quantidade} {quantidade === 1 ? 'linha' : 'linhas'}
          {capacidade > 0 && ` · o backend guarda as últimas ${capacidade}`}
        </span>
      </div>

      {erro && <p className="text-xs text-red-500">{erro}</p>}

      {/* Lista */}
      <div ref={lista} onScroll={aoRolar} className={`${cardBase} flex-1 min-h-[420px] overflow-auto font-mono text-[11px] leading-5`}>
        {!carregado && <p className="p-4 text-texto-3 font-sans text-xs">Carregando...</p>}
        {carregado && quantidade === 0 && (
          <p className="p-4 text-texto-3 font-sans text-xs">
            {texto || nivel ? 'Nenhuma linha com esse filtro.' : 'Nada registrado ainda.'}
          </p>
        )}
        {visiveis.map(l => (
          <div
            key={l.sequencia}
            onClick={l.excecao ? () => alternarExcecao(l.sequencia) : undefined}
            className={`px-3 py-0.5 border-b border-borda-suave ${l.excecao ? 'cursor-pointer hover:bg-superficie-2' : ''}`}
          >
            <div className="flex gap-3">
              <span className="text-texto-3 shrink-0">{horaComSegundos(l.instante)}</span>
              <span className={`w-12 shrink-0 ${COR_NIVEL[l.nivel] ?? 'text-texto-3'}`}>{SIGLA_NIVEL[l.nivel] ?? l.nivel}</span>
              <span className="text-accent-600 dark:text-accent-400 shrink-0 w-44 truncate" title={l.categoria}>{l.categoria}</span>
              <span className="text-texto whitespace-pre-wrap break-words min-w-0">
                {l.mensagem}
                {l.excecao && <span className="text-texto-3"> {abertas.has(l.sequencia) ? '▾' : '▸ exceção'}</span>}
              </span>
            </div>
            {l.excecao && abertas.has(l.sequencia) && (
              <pre className="ml-[8.5rem] my-1 p-2 bg-red-50 dark:bg-red-950/40 text-red-700 dark:text-red-300 whitespace-pre-wrap break-words text-[10px]">
                {l.excecao}
              </pre>
            )}
          </div>
        ))}
      </div>
    </div>
  )
}
