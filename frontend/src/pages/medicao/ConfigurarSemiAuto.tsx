// Configuração da medição Semi Automática (WISE), dentro do modal "Configurar medição".
// O usuário escolhe o WISE instalado na máquina na lista de cadastrados, com a situação de
// cada um (só em texto: conectado em verde, desconectado em amarelo, em uso em outra medição
// em vermelho); só dá para iniciar com ele conectado e livre. O WISE fica associado à máquina
// até finalizar a medição e depois fica livre para outra. O usuário escolhe
// o que ler: contadores de produção (S2, S5, S6) e refugo (S3), cada um com o seu
// multiplicador opcional (garrafas por ciclo), e os sensores (S1, S4, S7, S8) liga/desliga.
// Abre sempre no padrão: S2 e S3 sem multiplicador e os quatro sensores ligados.
// "Produção até então" (como no Manual): a leitura do contador da máquina ao iniciar; a
// produção do WISE soma a partir dela.
import { useEffect, useState, type ReactNode } from 'react'
import type { MaquinaLinha } from '../../types'
import { coletaIotService } from '../../services/coletaIotService'
import { dispositivoIotService, type WiseDto } from '../../services/dispositivoIotService'
import { entradasWiseService, type EntradaWiseDto } from '../../services/entradasWiseService'
import { mensagemErro } from '../../services/api'
import Switch from '../../components/Switch'
import WiseDaMedicao from '../../components/iot/WiseDaMedicao'
import { situacaoDoIp } from '../../components/iot/situacaoWise'
import { btnPrimary, btnSecondarySm } from '../../styles/buttons'
import { inputBase, inputMdFull, label, checkbox } from '../../styles/inputs'
import { modalBody, modalFooter } from '../../styles/modals'

const CONTADORES_PRODUCAO = ['S2', 'S5', 'S6']
const CONTADORES_REJEITO = ['S3']
const SENSORES = ['S1', 'S4', 'S7', 'S8']

// Nomes enquanto os textos da máquina não chegam (ou se a consulta falhar).
const NOMES_PADRAO: Record<string, string> = {
  S1: 'Acúmulo mínimo na entrada',
  S2: 'Contador de produção (entrada 1)',
  S3: 'Contador de refugo',
  S4: 'Acúmulo na saída (caixas/pallets)',
  S5: 'Contador de produção (entrada 2)',
  S6: 'Contador de produção (entrada 3)',
  S7: 'Acúmulo na saída de garrafas',
  S8: 'Falta de garrafas na entrada',
}

interface CanalConfig {
  ligado: boolean
  comMultiplicador: boolean
  multiplicador: string
}

function configuracaoPadrao(): Record<string, CanalConfig> {
  const ligados = new Set(['S2', 'S3', ...SENSORES])
  return Object.fromEntries(
    Object.keys(NOMES_PADRAO).map(c => [c, { ligado: ligados.has(c), comMultiplicador: false, multiplicador: '' }])
  )
}

const INTERVALO_WISE_MS = 5000

interface Props {
  maquina: MaquinaLinha
  // Seletor "Forma de medição" do modal, mostrado no topo
  seletorForma: ReactNode
  onCancelar: () => void
  onIniciada: (mensagem: string) => void
}

export default function ConfigurarSemiAuto({ maquina, seletorForma, onCancelar, onIniciada }: Props) {
  const [velocidadeNominal, setVelocidadeNominal] = useState(String(maquina.velocidadeNominal ?? ''))
  const [sobreVelocidade, setSobreVelocidade] = useState('0')
  const [producaoInicial, setProducaoInicial] = useState('')
  const [canais, setCanais] = useState<Record<string, CanalConfig>>(configuracaoPadrao)

  const [ipWise, setIpWise] = useState('')
  const [wises, setWises] = useState<WiseDto[] | null>(null)
  const [erroWises, setErroWises] = useState<string | null>(null)
  const [agora, setAgora] = useState(new Date())
  const [textos, setTextos] = useState<EntradaWiseDto[]>([])

  const [iniciando, setIniciando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  // WISE cadastrados e a situação de cada um (conectado, livre, em medição), atualizados
  // enquanto o modal está aberto.
  useEffect(() => {
    let ativo = true
    async function atualizar() {
      try {
        const lista = await dispositivoIotService.listar()
        if (!ativo) return
        setWises(lista)
        setErroWises(null)
      } catch (e) {
        if (ativo) setErroWises(mensagemErro(e, 'Não foi possível consultar os WISE.'))
      } finally {
        if (ativo) setAgora(new Date())
      }
    }
    atualizar()
    const id = setInterval(atualizar, INTERVALO_WISE_MS)
    return () => { ativo = false; clearInterval(id) }
  }, [])

  // Textos das entradas da máquina (catálogo): nomes dos canais.
  useEffect(() => {
    let ativo = true
    entradasWiseService.obter(maquina.maquinaId)
      .then(t => { if (ativo) setTextos(t) })
      .catch(() => { /* sem eles, ficam os nomes padrão */ })
    return () => { ativo = false }
  }, [maquina.maquinaId])

  function nome(canal: string) {
    return textos.find(e => e.canal === canal)?.nome ?? NOMES_PADRAO[canal]
  }

  function alterar(canal: string, mudanca: Partial<CanalConfig>) {
    setCanais(c => ({ ...c, [canal]: { ...c[canal], ...mudanca } }))
  }

  function multiplicadorValido(c: CanalConfig) {
    if (!c.ligado || !c.comMultiplicador) return true
    const n = Number(c.multiplicador)
    return Number.isInteger(n) && n >= 1
  }

  const semProducao = !CONTADORES_PRODUCAO.some(c => canais[c].ligado)
  const multiplicadorInvalido = [...CONTADORES_PRODUCAO, ...CONTADORES_REJEITO].some(c => !multiplicadorValido(canais[c]))
  const velocidadeInvalida = !(Number(velocidadeNominal) > 0)
  // Vazio = 0; senão um número inteiro, sem negativo.
  const producaoInicialInvalida = producaoInicial !== ''
    && !(Number.isInteger(Number(producaoInicial)) && Number(producaoInicial) >= 0)

  const wiseOk = wises !== null && wises.some(w => w.enderecoIp === ipWise && w.cadastrado)
    && situacaoDoIp(ipWise, wises).situacao === 'Conectado'
  const podeIniciar = wiseOk && !semProducao && !multiplicadorInvalido && !velocidadeInvalida && !producaoInicialInvalida && !iniciando

  async function iniciar() {
    setIniciando(true)
    setErro(null)
    try {
      await coletaIotService.iniciar({
        maquinaLinhaId: maquina.id,
        enderecoIpWise: ipWise.trim(),
        velocidadeNominal: Number(velocidadeNominal),
        sobreVelocidade: Number(sobreVelocidade) || 0,
        producaoInicial: maquina.medeProducao ? Number(producaoInicial) || 0 : null,
        canais: Object.entries(canais)
          .filter(([, c]) => c.ligado)
          .map(([canal, c]) => ({ canal, multiplicador: c.comMultiplicador ? Number(c.multiplicador) : 1 })),
      })
      onIniciada(`Coleta Semi Automática iniciada na ${maquina.maquinaNome} com o WISE ${ipWise.trim()}.`)
    } catch (e) {
      setErro(mensagemErro(e, 'Não foi possível iniciar a coleta.'))
    } finally {
      setIniciando(false)
    }
  }

  function linhaContador(canal: string) {
    const c = canais[canal]
    return (
      <div key={canal} className="grid grid-cols-[1fr_auto_5.5rem] items-center gap-3 py-1.5">
        <label className="flex items-center gap-2 min-w-0 text-xs text-zinc-900 dark:text-zinc-100">
          <input type="checkbox" checked={c.ligado} onChange={e => alterar(canal, { ligado: e.target.checked })} className={checkbox} />
          <span className="truncate">{canal} - {nome(canal)}</span>
        </label>
        <div className="flex items-center gap-2">
          <span className="text-[10px] text-zinc-400">Multiplicador</span>
          <Switch
            checked={c.comMultiplicador}
            disabled={!c.ligado}
            onChange={v => alterar(canal, { comMultiplicador: v, multiplicador: v ? c.multiplicador : '' })}
          />
        </div>
        {c.ligado && c.comMultiplicador ? (
          <input
            type="number" min="1" step="1"
            value={c.multiplicador}
            onChange={e => alterar(canal, { multiplicador: e.target.value })}
            placeholder="garrafas"
            title="Garrafas por ciclo (por pulso do contador)"
            className={`${inputBase} ${!multiplicadorValido(c) && c.multiplicador !== '' ? 'ring-1 ring-red-500' : ''}`}
          />
        ) : (
          <span />
        )}
      </div>
    )
  }

  return (
    <>
      <div className={modalBody}>
        {seletorForma}

        {erro && (
          <div className="bg-red-50 dark:bg-red-950 border border-red-200 dark:border-red-800 px-3 py-2 text-xs text-red-600 dark:text-red-400">
            {erro}
          </div>
        )}

        {/* WISE desta medição: escolhido da lista de cadastrados */}
        <WiseDaMedicao ip={ipWise} onChangeIp={setIpWise} wises={wises} erroLista={erroWises} agora={agora} />

        {/* Velocidade */}
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={label}>Velocidade nominal (garrafas/h)</label>
            <input type="number" min="0" value={velocidadeNominal} onChange={e => setVelocidadeNominal(e.target.value)} className={inputMdFull} />
          </div>
          <div>
            <label className={label}>Sobre velocidade (%)</label>
            <input type="number" min="0" max="100" value={sobreVelocidade} onChange={e => setSobreVelocidade(e.target.value)} className={inputMdFull} />
          </div>
        </div>

        {/* Produção até então — só faz sentido se a máquina mede produção */}
        {maquina.medeProducao && (
          <div>
            <label className={label}>Produção até então</label>
            <input
              type="number" min="0" step="1"
              value={producaoInicial}
              onChange={e => setProducaoInicial(e.target.value)}
              placeholder="Leitura atual do contador"
              className={`${inputMdFull} ${producaoInicialInvalida ? 'ring-1 ring-red-500' : ''}`}
            />
          </div>
        )}

        {/* Contadores */}
        <div>
          <label className="text-xs text-zinc-500 mb-1 block">Produção</label>
          {CONTADORES_PRODUCAO.map(linhaContador)}
          {semProducao && <p className="text-[10px] text-red-600 dark:text-red-400 mt-1">Marque ao menos um contador de produção.</p>}
        </div>
        <div>
          <label className="text-xs text-zinc-500 mb-1 block">Refugo</label>
          {CONTADORES_REJEITO.map(linhaContador)}
        </div>

        {/* Sensores */}
        <div>
          <label className="text-xs text-zinc-500 mb-1 block">Sensores</label>
          {SENSORES.map(canal => (
            <div key={canal} className="flex items-center justify-between gap-2 py-1.5">
              <span className="text-xs text-zinc-900 dark:text-zinc-100 truncate">{canal} - {nome(canal)}</span>
              <Switch checked={canais[canal].ligado} onChange={v => alterar(canal, { ligado: v })} />
            </div>
          ))}
        </div>
      </div>

      <div className={modalFooter}>
        <button onClick={onCancelar} disabled={iniciando} className={btnSecondarySm}>Cancelar</button>
        <button onClick={iniciar} disabled={!podeIniciar} className={btnPrimary}>
          {iniciando ? 'Iniciando...' : 'Iniciar coleta'}
        </button>
      </div>

    </>
  )
}
