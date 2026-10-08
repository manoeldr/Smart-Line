// Aba "Simulador de WISE" da Área do Desenvolvedor: liga WISE simulados dentro do próprio
// backend (publicam no broker como aparelhos reais, com IPs 127.0.0.21, .22…), com paradas e
// quedas de comunicação automáticas, e botões com os mesmos comandos do simulador de console.
// O formulário fica guardado no navegador para a próxima vez. Atualiza a cada 2 s.
import { useEffect, useState } from 'react'
import {
  desenvolvedorService,
  type AcaoSimulada,
  type CenarioSimulado,
  type MaquinaSimuladaDto,
  type OpcoesSimuladorDto,
  type SimuladorDto,
} from '../../services/desenvolvedorService'
import { mensagemErro } from '../../services/api'
import { btnPrimarySm, btnDangerSm } from '../../styles/buttons'
import { inputBase, checkbox, label as labelClass } from '../../styles/inputs'
import { cardPadded } from '../../styles/cards'
import { table, tableHeadRow, tableHeadCell, tableBodyRow, tableBodyCell, tableBodyCellMuted } from '../../styles/tables'

const INTERVALO_MS = 2000
const CHAVE_FORMULARIO = 'smartline.dev.simulador'

const OPCOES_PADRAO: OpcoesSimuladorDto = {
  maquinas: 2,
  intervaloSegundos: 20,
  garrafasPorMinuto: [600],
  garrafasPorPulso: [1],
  rejeitoPercentual: 1,
  primeiroIp: 21,
  automatico: true,
  rodandoMedioMin: 10,
  paradaMinimaMin: 1,
  paradaMaximaMin: 5,
  quedas: true,
  quedaMediaMin: 60,
  quedaMinimaMin: 3,
  quedaMaximaMin: 10,
  portaHttp: 80,
}

const NOME_CENARIO: Record<CenarioSimulado, string> = {
  Rodando: 'Rodando',
  FaltaGarrafas: 'Falta de garrafas',
  AbaixoAcumuloMinimo: 'Abaixo do acúmulo mínimo',
  SaidaGarrafasBloqueada: 'Saída de garrafas bloqueada',
  SaidaCaixasBloqueada: 'Saída de caixas bloqueada',
  ParadaSemCausa: 'Parada sem causa',
  Desligado: 'Sem energia',
}

// Botões de cenário: a ação e o cenário que ela produz (para destacar o atual).
const CENARIOS: { acao: AcaoSimulada; cenario: CenarioSimulado; label: string; titulo: string }[] = [
  { acao: 'rodando', cenario: 'Rodando', label: 'Rodando', titulo: 'Produzindo, sensores normais' },
  { acao: 'falta', cenario: 'FaltaGarrafas', label: 'Falta', titulo: 'Parada por falta de garrafas na entrada (S8)' },
  { acao: 'minimo', cenario: 'AbaixoAcumuloMinimo', label: 'Mínimo', titulo: 'Parada abaixo do acúmulo mínimo (S1)' },
  { acao: 'saida', cenario: 'SaidaGarrafasBloqueada', label: 'Saída', titulo: 'Parada com saída de garrafas bloqueada (S7)' },
  { acao: 'caixas', cenario: 'SaidaCaixasBloqueada', label: 'Caixas', titulo: 'Parada com saída de caixas bloqueada (S4)' },
  { acao: 'parada', cenario: 'ParadaSemCausa', label: 'Sem causa', titulo: 'Parada sem causa (fica sem classificação)' },
  { acao: 'desligar', cenario: 'Desligado', label: 'Sem energia', titulo: 'WISE sem energia: não comunica nem conta' },
]

function lerFormulario(): OpcoesSimuladorDto {
  try {
    const salvo = localStorage.getItem(CHAVE_FORMULARIO)
    if (salvo) return { ...OPCOES_PADRAO, ...JSON.parse(salvo) }
  } catch { /* sem armazenamento ou valor estranho: padrão */ }
  return OPCOES_PADRAO
}

function listaTexto(valores: number[] | null): string {
  return (valores ?? []).join(', ')
}

function textoLista(texto: string, padrao: number): number[] {
  const valores = texto.split(',').map(v => Number(v.trim().replace(',', '.'))).filter(v => Number.isFinite(v) && v > 0)
  return valores.length > 0 ? valores : [padrao]
}

function duracao(ateIso: string | null, agora: Date): string {
  if (!ateIso) return ''
  const s = Math.max(0, Math.round((new Date(ateIso).getTime() - agora.getTime()) / 1000))
  return s >= 60 ? `${Math.floor(s / 60)} min ${String(s % 60).padStart(2, '0')} s` : `${s} s`
}

function horaCurta(iso: string | null) {
  return iso ? new Date(iso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }) : ''
}

export default function AbaSimulador() {
  const [situacao, setSituacao] = useState<SimuladorDto | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [erroComando, setErroComando] = useState<string | null>(null)
  const [ocupado, setOcupado] = useState(false)
  const [agora, setAgora] = useState(new Date())

  // Formulário de ligar (texto cru das listas para o usuário digitar à vontade).
  const [form, setForm] = useState<OpcoesSimuladorDto>(lerFormulario)
  const [ritmoTexto, setRitmoTexto] = useState(() => listaTexto(lerFormulario().garrafasPorMinuto))
  const [pulsoTexto, setPulsoTexto] = useState(() => listaTexto(lerFormulario().garrafasPorPulso))

  useEffect(() => {
    let ativo = true
    async function carregar() {
      try {
        const s = await desenvolvedorService.simulador()
        if (!ativo) return
        setSituacao(s)
        setErro(null)
      } catch (e) {
        if (ativo) setErro(mensagemErro(e, 'Não foi possível consultar o simulador.'))
      } finally {
        if (ativo) setAgora(new Date())
      }
    }
    carregar()
    const id = setInterval(carregar, INTERVALO_MS)
    return () => { ativo = false; clearInterval(id) }
  }, [])

  function campo<K extends keyof OpcoesSimuladorDto>(nome: K, valor: OpcoesSimuladorDto[K]) {
    setForm(atual => ({ ...atual, [nome]: valor }))
  }

  function numero<K extends keyof OpcoesSimuladorDto>(nome: K, texto: string) {
    const v = Number(texto.replace(',', '.'))
    if (Number.isFinite(v)) campo(nome, v as OpcoesSimuladorDto[K])
  }

  async function executar(acao: () => Promise<SimuladorDto>, erroPadrao: string) {
    setOcupado(true)
    setErroComando(null)
    try {
      setSituacao(await acao())
      setAgora(new Date())
    } catch (e) {
      setErroComando(mensagemErro(e, erroPadrao))
    } finally {
      setOcupado(false)
    }
  }

  function ligar() {
    const opcoes: OpcoesSimuladorDto = {
      ...form,
      garrafasPorMinuto: textoLista(ritmoTexto, 600),
      garrafasPorPulso: textoLista(pulsoTexto, 1),
    }
    try { localStorage.setItem(CHAVE_FORMULARIO, JSON.stringify(opcoes)) } catch { /* sem armazenamento: segue */ }
    executar(() => desenvolvedorService.ligarSimulador(opcoes), 'Não foi possível ligar o simulador.')
  }

  const desligar = () => executar(() => desenvolvedorService.desligarSimulador(), 'Não foi possível desligar o simulador.')
  const comando = (n: number, acao: AcaoSimulada) =>
    executar(() => desenvolvedorService.comando(n, acao), 'O comando não foi aplicado.')

  if (!situacao && !erro) return <p className="text-xs text-zinc-400">Carregando...</p>

  const ligado = situacao?.ligado ?? false

  return (
    <div className="flex flex-col gap-4 max-w-5xl">
      {erro && <p className="text-xs text-red-500">{erro}</p>}

      {/* Cabeçalho: ligado/desligado */}
      <div className={`${cardPadded} flex items-center gap-4`}>
        <span className={`w-2.5 h-2.5 rounded-full shrink-0 ${ligado ? 'bg-emerald-500' : 'bg-zinc-300 dark:bg-zinc-600'}`} />
        <div className="flex-1 text-xs text-zinc-600 dark:text-zinc-300">
          {ligado && situacao ? (
            <>
              <span className="font-medium text-zinc-900 dark:text-zinc-100">Simulador ligado</span>
              {' '}desde {horaCurta(situacao.ligadoEm)} · {situacao.maquinas.length} WISE publicando
              no broker (porta {situacao.portaBroker}) a cada {situacao.opcoes?.intervaloSegundos} s
              {situacao.maquinas.some(m => m.interfaceHttp) && ` · interface HTTP na porta ${situacao.opcoes?.portaHttp}`}
            </>
          ) : (
            <>
              <span className="font-medium text-zinc-900 dark:text-zinc-100">Simulador desligado.</span>
              {' '}Ao ligar, cada WISE simulado aparece em Configurações → Dispositivos IoT com o IP abaixo, como um aparelho real.
            </>
          )}
        </div>
        {ligado ? (
          <button onClick={desligar} disabled={ocupado} className={btnDangerSm}>Desligar</button>
        ) : (
          <button onClick={ligar} disabled={ocupado} className={btnPrimarySm}>Ligar simulador</button>
        )}
      </div>

      {erroComando && <p className="text-xs text-red-500">{erroComando}</p>}

      {/* Formulário (só desligado) */}
      {!ligado && (
        <div className={`${cardPadded} grid grid-cols-2 md:grid-cols-4 gap-x-4 gap-y-3`}>
          <div>
            <label className={labelClass}>Máquinas (1 a 50)</label>
            <input type="number" min={1} max={50} className={inputBase} value={form.maquinas} onChange={e => numero('maquinas', e.target.value)} />
          </div>
          <div>
            <label className={labelClass}>Publicação a cada (s)</label>
            <input type="number" min={1} max={3600} className={inputBase} value={form.intervaloSegundos} onChange={e => numero('intervaloSegundos', e.target.value)} />
          </div>
          <div>
            <label className={labelClass}>Garrafas por minuto (um valor ou um por máquina)</label>
            <input className={inputBase} value={ritmoTexto} onChange={e => setRitmoTexto(e.target.value)} placeholder="600 ou 600, 600, 500" />
          </div>
          <div>
            <label className={labelClass}>Garrafas por pulso (um por máquina)</label>
            <input className={inputBase} value={pulsoTexto} onChange={e => setPulsoTexto(e.target.value)} placeholder="1 ou 1, 1, 84" />
          </div>
          <div>
            <label className={labelClass}>Refugo (% da produção)</label>
            <input type="number" min={0} max={100} step="0.1" className={inputBase} value={form.rejeitoPercentual} onChange={e => numero('rejeitoPercentual', e.target.value)} />
          </div>
          <div>
            <label className={labelClass}>Primeiro IP (127.0.0.N)</label>
            <input type="number" min={2} max={200} className={inputBase} value={form.primeiroIp} onChange={e => numero('primeiroIp', e.target.value)} />
          </div>
          <div>
            <label className={labelClass}>Porta HTTP dos WISE (0 desliga)</label>
            <input type="number" min={0} max={65535} className={inputBase} value={form.portaHttp} onChange={e => numero('portaHttp', e.target.value)} />
            <p className="text-[10px] text-zinc-400 mt-1">A mesma de WISE_HTTP_PORTA no backend (zerar contadores).</p>
          </div>
          <div className="flex flex-col gap-2 pt-5">
            <label className="flex items-center gap-1.5 text-xs text-zinc-600 dark:text-zinc-300">
              <input type="checkbox" className={checkbox} checked={form.automatico} onChange={e => campo('automatico', e.target.checked)} />
              Paradas automáticas
            </label>
            <label className="flex items-center gap-1.5 text-xs text-zinc-600 dark:text-zinc-300">
              <input type="checkbox" className={checkbox} checked={form.quedas} onChange={e => campo('quedas', e.target.checked)} disabled={!form.automatico} />
              Quedas de comunicação automáticas
            </label>
          </div>

          <div className="col-span-2 md:col-span-4 grid grid-cols-3 md:grid-cols-6 gap-x-4 gap-y-3 pt-2 border-t border-zinc-100 dark:border-zinc-800">
            <div>
              <label className={labelClass}>Rodando entre paradas (min, médio)</label>
              <input type="number" min={0.1} step="0.5" className={inputBase} value={form.rodandoMedioMin} onChange={e => numero('rodandoMedioMin', e.target.value)} disabled={!form.automatico} />
            </div>
            <div>
              <label className={labelClass}>Parada mínima (min)</label>
              <input type="number" min={0.1} step="0.5" className={inputBase} value={form.paradaMinimaMin} onChange={e => numero('paradaMinimaMin', e.target.value)} disabled={!form.automatico} />
            </div>
            <div>
              <label className={labelClass}>Parada máxima (min)</label>
              <input type="number" min={0.1} step="0.5" className={inputBase} value={form.paradaMaximaMin} onChange={e => numero('paradaMaximaMin', e.target.value)} disabled={!form.automatico} />
            </div>
            <div>
              <label className={labelClass}>Comunicando entre quedas (min, médio)</label>
              <input type="number" min={0.5} step="1" className={inputBase} value={form.quedaMediaMin} onChange={e => numero('quedaMediaMin', e.target.value)} disabled={!form.automatico || !form.quedas} />
            </div>
            <div>
              <label className={labelClass}>Queda mínima (min)</label>
              <input type="number" min={0.1} step="0.5" className={inputBase} value={form.quedaMinimaMin} onChange={e => numero('quedaMinimaMin', e.target.value)} disabled={!form.automatico || !form.quedas} />
            </div>
            <div>
              <label className={labelClass}>Queda máxima (min)</label>
              <input type="number" min={0.1} step="0.5" className={inputBase} value={form.quedaMaximaMin} onChange={e => numero('quedaMaximaMin', e.target.value)} disabled={!form.automatico || !form.quedas} />
            </div>
          </div>
        </div>
      )}

      {/* Máquinas (só ligado) */}
      {ligado && situacao && (
        <div className={cardPadded}>
          <div className="flex items-center gap-3 mb-3">
            <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">Máquinas simuladas</p>
            <span className="text-[10px] text-zinc-400">Cadastre cada IP em Configurações → Dispositivos IoT e escolha o WISE ao iniciar a medição.</span>
            <div className="ml-auto flex items-center gap-1">
              <span className="text-[10px] text-zinc-400 mr-1">Todas:</span>
              <BotoesComando maquina={null} ocupado={ocupado} onComando={acao => comando(0, acao)} />
            </div>
          </div>
          <table className={table}>
            <thead>
              <tr className={tableHeadRow}>
                <th className={tableHeadCell}>Máquina</th>
                <th className={tableHeadCell}>IP</th>
                <th className={tableHeadCell}>Situação</th>
                <th className={tableHeadCell}>Contadores</th>
                <th className={tableHeadCell}>Modo</th>
                <th className={tableHeadCell}>Comandos</th>
              </tr>
            </thead>
            <tbody>
              {situacao.maquinas.map(m => (
                <tr key={m.numero} className={tableBodyRow}>
                  <td className={tableBodyCell}>
                    {m.numero}
                    <span className="block text-[10px] text-zinc-400">
                      {m.garrafasPorMinuto} garrafas/min{m.garrafasPorPulso > 1 && ` · ${m.garrafasPorPulso} por pulso`}
                    </span>
                  </td>
                  <td className={tableBodyCell}>
                    <span className="font-mono">{m.enderecoIp}</span>
                    <span className={`block text-[10px] ${m.conectada ? 'text-emerald-600 dark:text-emerald-400' : 'text-zinc-400'}`}>
                      {m.conectada ? 'conectado ao broker' : 'sem conexão'}
                    </span>
                  </td>
                  <td className={tableBodyCell}>
                    <SituacaoMaquina m={m} />
                  </td>
                  <td className={tableBodyCellMuted}>
                    <span className="font-mono">S2={m.contadorProducao} S3={m.contadorRejeito}</span>
                  </td>
                  <td className={tableBodyCellMuted}>
                    {m.automatico ? (
                      <>
                        automático
                        {m.proximaMudanca && (
                          <span className="block text-[10px]">
                            {m.cenario === 'Rodando' ? 'para' : 'volta'} em {duracao(m.proximaMudanca, agora)}
                          </span>
                        )}
                        {m.proximaMudancaComunicacao && (
                          <span className="block text-[10px]">
                            {m.falha ? 'comunicação volta' : 'queda'} em {duracao(m.proximaMudancaComunicacao, agora)}
                          </span>
                        )}
                      </>
                    ) : 'só por comando'}
                  </td>
                  <td className={`${tableBodyCell} w-[46%]`}>
                    <BotoesComando maquina={m} ocupado={ocupado} onComando={acao => comando(m.numero, acao)} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function SituacaoMaquina({ m }: { m: MaquinaSimuladaDto }) {
  const rodando = m.cenario === 'Rodando' && !m.falha
  const cor = m.falha
    ? 'text-zinc-500'
    : rodando ? 'text-emerald-600 dark:text-emerald-400' : 'text-amber-600 dark:text-amber-400'
  return (
    <span className={cor}>
      {NOME_CENARIO[m.cenario] ?? m.cenario}
      {m.falha === 'SemRede' && <span className="block text-[10px]">sem rede (continua contando)</span>}
      {m.falha === 'SemEnergia' && <span className="block text-[10px]">sem energia (não conta)</span>}
    </span>
  )
}

// Os comandos do console como botões. Com uma máquina, destaca o cenário em que ela está;
// para "Todas" (maquina nula) nada fica destacado.
function BotoesComando({ maquina, ocupado, onComando }: {
  maquina: MaquinaSimuladaDto | null
  ocupado: boolean
  onComando: (acao: AcaoSimulada) => void
}) {
  const botao = (ativo: boolean, tom: 'normal' | 'atencao' = 'normal') =>
    `h-6 px-1.5 text-[10px] border transition-colors disabled:opacity-40 disabled:cursor-not-allowed ${
      ativo
        ? 'bg-blue-600 text-white border-blue-600'
        : tom === 'atencao'
          ? 'border-amber-300 dark:border-amber-700 text-amber-700 dark:text-amber-400 hover:bg-amber-50 dark:hover:bg-amber-950'
          : 'border-zinc-200 dark:border-zinc-700 text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800'
    }`
  return (
    <div className="flex flex-wrap gap-1">
      {CENARIOS.map(c => (
        <button
          key={c.acao}
          title={c.titulo}
          disabled={ocupado}
          onClick={() => onComando(c.acao)}
          className={botao(maquina !== null && maquina.cenario === c.cenario && !maquina.falha)}
        >
          {c.label}
        </button>
      ))}
      <button title="A rede cai: o WISE continua contando, mas não publica" disabled={ocupado} onClick={() => onComando('semrede')}
        className={botao(maquina?.falha === 'SemRede', 'atencao')}>
        Sem rede
      </button>
      <button title="O WISE reinicia: contadores voltam a zero" disabled={ocupado} onClick={() => onComando('reiniciar')}
        className={botao(false, 'atencao')}>
        Reiniciar
      </button>
      {maquina === null || !maquina.automatico ? (
        <button title="Paradas e quedas de comunicação automáticas" disabled={ocupado} onClick={() => onComando('auto')} className={botao(false)}>
          Automático
        </button>
      ) : (
        <button title="Só por comando (desliga as automáticas)" disabled={ocupado} onClick={() => onComando('manual')} className={botao(false)}>
          Manual
        </button>
      )}
      {maquina === null && (
        <button title="Só por comando (desliga as automáticas)" disabled={ocupado} onClick={() => onComando('manual')} className={botao(false)}>
          Manual
        </button>
      )}
    </div>
  )
}
