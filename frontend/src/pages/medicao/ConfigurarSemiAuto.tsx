// Configuração da medição Semi Automática (WISE), dentro do modal "Configurar medição".
// Mostra a situação do WISE da máquina (só em texto: conectado em verde, desconectado em
// amarelo, não cadastrado em vermelho) e só deixa iniciar com ele conectado. O usuário escolhe
// o que ler: contadores de produção (S2, S5, S6) e rejeito (S3), cada um com o seu
// multiplicador opcional (garrafas por ciclo), e os sensores (S1, S4, S7, S8) liga/desliga.
// Abre sempre no padrão: S2 e S3 sem multiplicador e os quatro sensores ligados.
import { useEffect, useState, type ReactNode } from 'react'
import type { MaquinaLinha } from '../../types'
import { coletaIotService } from '../../services/coletaIotService'
import type { SituacaoWiseDto } from '../../services/dispositivoIotService'
import { regrasClassificacaoService, type RegraDto } from '../../services/regrasClassificacaoService'
import { mensagemErro } from '../../services/api'
import SituacaoWiseTexto from '../../components/iot/SituacaoWiseTexto'
import Switch from '../../components/Switch'
import ValidarEntradasModal from '../../modals/ValidarEntradasModal'
import { tempoDesde } from '../../utils/tempo'
import { btnPrimary, btnSecondarySm } from '../../styles/buttons'
import { inputBase, inputMdFull, label, checkbox } from '../../styles/inputs'
import { modalBody, modalFooter } from '../../styles/modals'

const CONTADORES_PRODUCAO = ['S2', 'S5', 'S6']
const CONTADORES_REJEITO = ['S3']
const SENSORES = ['S1', 'S4', 'S7', 'S8']

// Nomes até a primeira resposta do WISE (depois valem os textos da máquina).
const NOMES_PADRAO: Record<string, string> = {
  S1: 'Acúmulo mínimo na entrada',
  S2: 'Contador de produção (entrada 1)',
  S3: 'Contador de rejeito',
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
  const [canais, setCanais] = useState<Record<string, CanalConfig>>(configuracaoPadrao)

  const [wise, setWise] = useState<SituacaoWiseDto | null>(null)
  const [erroWise, setErroWise] = useState<string | null>(null)
  const [agora, setAgora] = useState(new Date())
  const [regras, setRegras] = useState<RegraDto[]>([])
  const [validando, setValidando] = useState(false)

  const [iniciando, setIniciando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  // Situação do WISE, atualizada enquanto o modal está aberto.
  useEffect(() => {
    let ativo = true
    async function atualizar() {
      try {
        const s = await coletaIotService.wiseDaMaquina(maquina.id)
        if (!ativo) return
        setWise(s)
        setErroWise(null)
      } catch (e) {
        if (ativo) setErroWise(mensagemErro(e, 'Não foi possível consultar o WISE da máquina.'))
      } finally {
        if (ativo) setAgora(new Date())
      }
    }
    atualizar()
    const id = setInterval(atualizar, INTERVALO_WISE_MS)
    return () => { ativo = false; clearInterval(id) }
  }, [maquina.id])

  // Regras da máquina: para avisar quando um sensor desligado é usado por alguma.
  useEffect(() => {
    let ativo = true
    regrasClassificacaoService.daMaquinaLinha(maquina.id)
      .then(r => { if (ativo) setRegras(r.regras) })
      .catch(() => { /* sem as regras, só não mostra o aviso */ })
    return () => { ativo = false }
  }, [maquina.id])

  function nome(canal: string) {
    return wise?.entradas.find(e => e.canal === canal)?.nome ?? NOMES_PADRAO[canal]
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

  const sensoresDesligados = new Set(SENSORES.filter(c => !canais[c].ligado))
  const regrasAfetadas = regras.filter(r => r.ativa && r.condicoes.some(c => c.canal && sensoresDesligados.has(c.canal)))

  const podeIniciar = wise?.situacao === 'Conectado' && !semProducao && !multiplicadorInvalido && !velocidadeInvalida && !iniciando

  async function iniciar() {
    setIniciando(true)
    setErro(null)
    try {
      await coletaIotService.iniciar({
        maquinaLinhaId: maquina.id,
        velocidadeNominal: Number(velocidadeNominal),
        sobreVelocidade: Number(sobreVelocidade) || 0,
        canais: Object.entries(canais)
          .filter(([, c]) => c.ligado)
          .map(([canal, c]) => ({ canal, multiplicador: c.comMultiplicador ? Number(c.multiplicador) : 1 })),
      })
      onIniciada(`Coleta Semi Automática iniciada na ${maquina.maquinaNome}.`)
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
          <span className="text-[10px] text-zinc-400 text-right">{c.ligado ? '1 garrafa por pulso' : ''}</span>
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

        {/* WISE da máquina */}
        <div className="border border-zinc-200 dark:border-zinc-800 px-3 py-2.5 flex items-center justify-between gap-3">
          <div className="text-xs min-w-0">
            {!wise ? (
              <p className="text-zinc-400">{erroWise ?? 'Consultando o WISE...'}</p>
            ) : (
              <>
                <p className="text-zinc-500">
                  WISE {wise.enderecoIp ? `${wise.enderecoIp} ` : ''}<SituacaoWiseTexto situacao={wise.situacao} />
                </p>
                <p className="text-[10px] text-zinc-400 mt-0.5">
                  {wise.situacao === 'NaoCadastrado'
                    ? 'Cadastre o WISE desta máquina em Configurações > Dispositivos IoT.'
                    : wise.situacao === 'Desconectado'
                      ? 'Verifique energia, rede e a configuração MQTT do WISE.'
                      : `última mensagem ${tempoDesde(wise.ultimaMensagemUtc, agora)}`}
                </p>
              </>
            )}
          </div>
          <button onClick={() => setValidando(true)} disabled={wise?.situacao !== 'Conectado'} className={btnSecondarySm}>
            Validar entradas
          </button>
        </div>

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

        {/* Contadores */}
        <div>
          <label className="text-xs text-zinc-500 mb-1 block">Produção</label>
          {CONTADORES_PRODUCAO.map(linhaContador)}
          {semProducao && <p className="text-[10px] text-red-600 dark:text-red-400 mt-1">Marque ao menos um contador de produção.</p>}
        </div>
        <div>
          <label className="text-xs text-zinc-500 mb-1 block">Rejeito</label>
          {CONTADORES_REJEITO.map(linhaContador)}
          {multiplicadorInvalido && (
            <p className="text-[10px] text-red-600 dark:text-red-400 mt-1">Garrafas por ciclo deve ser um número inteiro maior que zero.</p>
          )}
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
          {regrasAfetadas.map(r => (
            <p key={r.id} className="text-[10px] text-amber-600 dark:text-amber-400 mt-1">
              A regra "{r.nome}" usa {r.condicoes.filter(c => c.canal && sensoresDesligados.has(c.canal)).map(c => c.canal).join(', ')},
              que está desligado: ela não vai classificar paradas nesta medição.
            </p>
          ))}
        </div>

        <p className="text-[10px] text-zinc-400">
          A coleta é contínua: vira o dia à meia-noite e só termina quando alguém finalizar. As paradas são classificadas pelas regras da máquina.
        </p>
      </div>

      <div className={modalFooter}>
        <button onClick={onCancelar} disabled={iniciando} className={btnSecondarySm}>Cancelar</button>
        <button onClick={iniciar} disabled={!podeIniciar} className={btnPrimary}>
          {iniciando ? 'Iniciando...' : 'Iniciar coleta'}
        </button>
      </div>

      <ValidarEntradasModal
        open={validando}
        subtitulo={`${maquina.maquinaNome}${wise?.enderecoIp ? ` · IP ${wise.enderecoIp}` : ''}`}
        carregar={() => coletaIotService.wiseDaMaquina(maquina.id)}
        onFechar={() => setValidando(false)}
      />
    </>
  )
}
