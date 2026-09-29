// Aba "Semi Automático" do modal da máquina (catálogo): textos das entradas do WISE e as
// regras padrão que dão o motivo das paradas. Segue o "staged changes" do modal: nada vai
// para o banco até o Salvar do modal (que chama a função registrada em salvarRef).
// "Restaurar padrão" é a exceção: pede confirmação e grava na hora.
import { useEffect, useState, type RefObject } from 'react'
import { entradasWiseService, type EntradaWiseDto } from '../../services/entradasWiseService'
import { regrasClassificacaoService, type ConjuntoRegrasDto } from '../../services/regrasClassificacaoService'
import { maquinaService } from '../../services/maquinaService'
import { mensagemErro } from '../../services/api'
import EditorRegras from '../../components/iot/EditorRegras'
import { paraEdicao, paraSalvar, validarRegras, type MotivoOpcao, type RegraEdit } from '../../components/iot/regrasEdicao'
import ConfirmModal from '../../components/ConfirmModal'
import { TAMANHO_MAXIMO_TEXTO, TEXTOS_PADRAO_WISE, type TextoEntrada } from '../../utils/entradasWise'
import { btnSecondarySm } from '../../styles/buttons'
import { inputBase } from '../../styles/inputs'

interface Props {
  maquinaId: string
  // O modal chama ao Salvar; a aba registra aqui a sua gravação
  salvarRef: RefObject<(() => Promise<void>) | null>
}

type Restaurar = 'entradas' | 'regras' | null

function textosDe(entradas: EntradaWiseDto[]): Record<string, TextoEntrada> {
  return Object.fromEntries(entradas.map(e => [e.canal, { nome: e.nome, ativo: e.textoAtivo, normal: e.textoNormal }]))
}

// Motivos da máquina + os das regras que não vierem na lista (inativos)
function motivosDe(ativos: { id: string; nome: string; tipo: string }[], conjunto: ConjuntoRegrasDto): MotivoOpcao[] {
  const lista: MotivoOpcao[] = ativos.filter(m => m.tipo !== 'Planejada').map(m => ({ ...m, ativo: true }))
  for (const r of conjunto.regras) {
    if (!lista.some(m => m.id === r.motivoParadaId)) {
      lista.push({ id: r.motivoParadaId, nome: r.motivo, tipo: r.tipo, ativo: r.motivoAtivo })
    }
  }
  return lista
}

export default function SemiAutomaticoMaquina({ maquinaId, salvarRef }: Props) {
  const [entradas, setEntradas] = useState<EntradaWiseDto[] | null>(null)
  const [textos, setTextos] = useState<Record<string, TextoEntrada>>({})
  const [regras, setRegras] = useState<RegraEdit[]>([])
  const [motivos, setMotivos] = useState<MotivoOpcao[]>([])
  const [erro, setErro] = useState<string | null>(null)

  const [textosAlterados, setTextosAlterados] = useState(false)
  const [regrasAlteradas, setRegrasAlteradas] = useState(false)
  const [restaurar, setRestaurar] = useState<Restaurar>(null)

  useEffect(() => {
    let ativo = true
    Promise.all([
      entradasWiseService.obter(maquinaId),
      regrasClassificacaoService.doCatalogo(maquinaId),
      maquinaService.getMotivosParada(maquinaId),
    ])
      .then(([e, r, m]) => {
        if (!ativo) return
        setEntradas(e)
        setTextos(textosDe(e))
        setRegras(paraEdicao(r.regras))
        setMotivos(motivosDe(m, r))
      })
      .catch(e => { if (ativo) setErro(mensagemErro(e, 'Erro ao carregar a configuração do Semi Automático.')) })
    return () => { ativo = false }
  }, [maquinaId])

  // Gravação chamada pelo Salvar do modal. Só manda o que mudou.
  useEffect(() => {
    salvarRef.current = async () => {
      if (regrasAlteradas) {
        const problema = validarRegras(regras)
        if (problema) throw new Error(`Semi Automático: ${problema}`)
      }
      if (textosAlterados) {
        const problema = validarTextos(textos)
        if (problema) throw new Error(`Semi Automático: ${problema}`)
        await entradasWiseService.salvar(maquinaId, Object.entries(textos).map(([canal, t]) => ({
          canal, nome: t.nome.trim(), textoAtivo: t.ativo?.trim() ?? null, textoNormal: t.normal?.trim() ?? null,
        })))
      }
      if (regrasAlteradas) {
        await regrasClassificacaoService.salvarDoCatalogo(maquinaId, paraSalvar(regras))
      }
    }
    return () => { salvarRef.current = null }
  })

  function alterarTexto(canal: string, mudanca: Partial<TextoEntrada>) {
    setTextos(t => ({ ...t, [canal]: { ...t[canal], ...mudanca } }))
    setTextosAlterados(true)
  }

  function alterarRegras(novas: RegraEdit[]) {
    setRegras(novas)
    setRegrasAlteradas(true)
  }

  async function confirmarRestaurar() {
    const alvo = restaurar
    setRestaurar(null)
    setErro(null)
    try {
      if (alvo === 'entradas') {
        const e = await entradasWiseService.restaurarPadrao(maquinaId)
        setEntradas(e)
        setTextos(textosDe(e))
        setTextosAlterados(false)
      } else if (alvo === 'regras') {
        const r = await regrasClassificacaoService.restaurarPadraoDoCatalogo(maquinaId)
        const m = await maquinaService.getMotivosParada(maquinaId)
        setRegras(paraEdicao(r.regras))
        setMotivos(motivosDe(m, r))
        setRegrasAlteradas(false)
      }
    } catch (e) {
      setErro(mensagemErro(e, 'Não foi possível restaurar o padrão.'))
    }
  }

  if (erro && !entradas) return <p className="text-xs text-red-600 dark:text-red-400">{erro}</p>
  if (!entradas) return <p className="text-xs text-zinc-400">Carregando...</p>

  return (
    <div className="flex flex-col gap-6">
      {erro && <p className="text-xs text-red-600 dark:text-red-400">{erro}</p>}

      {/* Entradas do WISE */}
      <div>
        <div className="flex items-center justify-between mb-1">
          <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">Entradas do WISE</p>
          <button onClick={() => setRestaurar('entradas')} className={btnSecondarySm}>Restaurar textos padrão</button>
        </div>
        <p className="text-[10px] text-zinc-400 mb-2">
          Como cada entrada aparece nas regras, no Validar entradas e no detalhe da máquina. Mudar o texto não muda o que é alarme em cada sensor.
        </p>
        <table className="w-full text-xs">
          <thead>
            <tr className="border-b border-zinc-200 dark:border-zinc-800 text-left text-zinc-400">
              <th className="py-1.5 pr-2 font-medium w-20">Entrada</th>
              <th className="py-1.5 pr-2 font-medium">Nome</th>
              <th className="py-1.5 pr-2 font-medium">Quando ativo</th>
              <th className="py-1.5 font-medium">Quando normal</th>
            </tr>
          </thead>
          <tbody>
            {entradas.map(e => {
              const t = textos[e.canal]
              const sensor = e.tipo === 'Estado'
              return (
                <tr key={e.canal} className="border-b border-zinc-100 dark:border-zinc-800">
                  <td className="py-1.5 pr-2 text-zinc-500 whitespace-nowrap">{e.canal} (DI{e.entrada})</td>
                  <td className="py-1.5 pr-2">
                    <input value={t.nome} maxLength={TAMANHO_MAXIMO_TEXTO} onChange={x => alterarTexto(e.canal, { nome: x.target.value })} className={inputBase} />
                  </td>
                  <td className="py-1.5 pr-2">
                    {sensor
                      ? <input value={t.ativo ?? ''} maxLength={TAMANHO_MAXIMO_TEXTO} onChange={x => alterarTexto(e.canal, { ativo: x.target.value })} className={inputBase} />
                      : <span className="text-[10px] text-zinc-400">contador</span>}
                  </td>
                  <td className="py-1.5">
                    {sensor && <input value={t.normal ?? ''} maxLength={TAMANHO_MAXIMO_TEXTO} onChange={x => alterarTexto(e.canal, { normal: x.target.value })} className={inputBase} />}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      {/* Regras */}
      <div>
        <div className="flex items-center justify-between mb-1">
          <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">Regras de classificação (padrão desta máquina)</p>
          <button onClick={() => setRestaurar('regras')} className={btnSecondarySm}>Restaurar regras padrão</button>
        </div>
        <p className="text-[10px] text-zinc-400 mb-2">
          Quando a máquina para, as regras são testadas de cima para baixo: a primeira com todas as condições verdadeiras dá o motivo.
          Sem nenhuma, a parada fica sem motivo (conta como interna). Vale para as coletas iniciadas depois de salvar; uma máquina da linha pode ter regras próprias.
        </p>
        <EditorRegras regras={regras} onChange={alterarRegras} motivos={motivos} textos={textos} />
        <p className="text-[10px] text-zinc-400 mt-2">Motivos novos criados na aba Manual aparecem aqui depois de salvar a máquina.</p>
      </div>

      <ConfirmModal
        open={restaurar !== null}
        titulo={restaurar === 'entradas' ? 'Restaurar textos padrão' : 'Restaurar regras padrão'}
        mensagem={restaurar === 'entradas'
          ? 'Os textos das 8 entradas desta máquina voltam ao padrão agora. Continuar?'
          : 'As regras desta máquina voltam às 4 regras padrão agora (as alterações não salvas se perdem). Continuar?'}
        onConfirmar={confirmarRestaurar}
        onCancelar={() => setRestaurar(null)}
      />
    </div>
  )
}

function validarTextos(textos: Record<string, TextoEntrada>): string | null {
  for (const [canal, t] of Object.entries(textos)) {
    if (!t.nome.trim()) return `${canal}: informe o nome da entrada.`
    const sensor = TEXTOS_PADRAO_WISE[canal]?.ativo !== null
    if (sensor && (!t.ativo?.trim() || !t.normal?.trim())) return `${canal}: informe o texto de ativo e o de normal.`
  }
  return null
}
