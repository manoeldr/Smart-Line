// Semi Automático de uma máquina da linha: tempo para detectar parada (Z) e as regras de
// classificação — as do catálogo ou personalizadas só para esta máquina. O WISE não é da
// máquina: é informado ao iniciar cada medição. Grava ao Salvar deste modal (não depende do Salvar
// do cliente). Vale para as coletas iniciadas depois.
import { useEffect, useState } from 'react'
import type { MaquinaLinhaConfDto } from '../services/linhaMaquinaService'
import { linhaMaquinaService } from '../services/linhaMaquinaService'
import { regrasClassificacaoService, type RegraDto } from '../services/regrasClassificacaoService'
import { entradasWiseService } from '../services/entradasWiseService'
import { maquinaService } from '../services/maquinaService'
import { mensagemErro } from '../services/api'
import EditorRegras from '../components/iot/EditorRegras'
import { paraEdicao, paraSalvar, textoCondicao, validarRegras, type MotivoOpcao, type RegraEdit } from '../components/iot/regrasEdicao'
import { TEXTOS_PADRAO_WISE, type TextoEntrada } from '../utils/entradasWise'
import { btnPrimary, btnSecondarySm } from '../styles/buttons'
import { inputBase, label } from '../styles/inputs'
import { modalOverlayNested, modalPanel, modalHeader, modalTitle, modalSubtitle, modalBody, modalFooter } from '../styles/modals'

const Z_MINIMO = 10
const Z_MAXIMO = 3600

interface Props {
  item: MaquinaLinhaConfDto
  onFechar: () => void
  onSalvo: (mudanca: { tempoDeteccaoParadaSegundos: number; regrasPersonalizadas: boolean }) => void
}

export default function MaquinaLinhaSemiModal({ item, onFechar, onSalvo }: Props) {
  const [z, setZ] = useState(String(item.tempoDeteccaoParadaSegundos ?? 60))
  const [textos, setTextos] = useState<Record<string, TextoEntrada>>(TEXTOS_PADRAO_WISE)
  const [motivos, setMotivos] = useState<MotivoOpcao[]>([])

  // Regras: as do catálogo (só leitura) ou as personalizadas (editáveis)
  const [personalizadoNoBanco, setPersonalizadoNoBanco] = useState(false)
  const [personalizado, setPersonalizado] = useState(false)
  const [regrasCatalogo, setRegrasCatalogo] = useState<RegraDto[]>([])
  const [regras, setRegras] = useState<RegraEdit[]>([])
  const [carregado, setCarregado] = useState(false)

  const [salvando, setSalvando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  useEffect(() => {
    let ativo = true
    Promise.all([
      regrasClassificacaoService.daMaquinaLinha(item.id),
      regrasClassificacaoService.doCatalogo(item.maquinaId),
      entradasWiseService.obter(item.maquinaId),
      maquinaService.getMotivosParada(item.maquinaId),
    ])
      .then(([vigentes, catalogo, entradas, m]) => {
        if (!ativo) return
        setPersonalizadoNoBanco(vigentes.personalizado)
        setPersonalizado(vigentes.personalizado)
        setRegrasCatalogo(catalogo.regras)
        setRegras(paraEdicao(vigentes.regras))
        setTextos(Object.fromEntries(entradas.map(e => [e.canal, { nome: e.nome, ativo: e.textoAtivo, normal: e.textoNormal }])))
        const lista: MotivoOpcao[] = m.filter(x => x.tipo !== 'Planejada').map(x => ({ ...x, ativo: true }))
        for (const r of [...vigentes.regras, ...catalogo.regras]) {
          if (!lista.some(x => x.id === r.motivoParadaId)) lista.push({ id: r.motivoParadaId, nome: r.motivo, tipo: r.tipo, ativo: r.motivoAtivo })
        }
        setMotivos(lista)
        setCarregado(true)
      })
      .catch(e => { if (ativo) setErro(mensagemErro(e, 'Erro ao carregar a configuração desta máquina.')) })
    return () => { ativo = false }
  }, [item.id, item.maquinaId])

  function personalizar() {
    // Começa de uma cópia das regras do catálogo
    setRegras(paraEdicao(regrasCatalogo))
    setPersonalizado(true)
  }

  const zNumero = Number(z)
  const zValido = Number.isInteger(zNumero) && zNumero >= Z_MINIMO && zNumero <= Z_MAXIMO

  async function salvar() {
    setErro(null)
    if (!zValido) { setErro(`Tempo para detectar parada deve ficar entre ${Z_MINIMO} e ${Z_MAXIMO} segundos.`); return }
    if (personalizado) {
      const problema = validarRegras(regras)
      if (problema) { setErro(problema); return }
    }

    setSalvando(true)
    try {
      await linhaMaquinaService.atualizar(item.linhaId, item.id, item.critica, item.velocidadeNominal, item.sobreVelocidade, item.medeProducao, zNumero)
      if (personalizado) {
        await regrasClassificacaoService.salvarDaMaquinaLinha(item.id, paraSalvar(regras))
      } else if (personalizadoNoBanco) {
        await regrasClassificacaoService.removerPersonalizacao(item.id)
      }
      onSalvo({ tempoDeteccaoParadaSegundos: zNumero, regrasPersonalizadas: personalizado })
    } catch (e) {
      setErro(mensagemErro(e, 'Não foi possível salvar.'))
    } finally {
      setSalvando(false)
    }
  }

  return (
    <div className={modalOverlayNested}>
      <div className={`${modalPanel} w-[820px] max-h-[90vh]`}>
        <div className={modalHeader}>
          <p className={modalTitle}>Semi Automático · {item.maquinaNome}</p>
          <p className={modalSubtitle}>Vale para as coletas iniciadas depois de salvar.</p>
        </div>

        <div className={modalBody}>
          {erro && (
            <div className="bg-red-50 dark:bg-red-950 border border-red-200 dark:border-red-800 px-3 py-2 text-xs text-red-600 dark:text-red-400">
              {erro}
            </div>
          )}

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className={label}>Tempo para detectar parada (segundos)</label>
              <input type="number" min={Z_MINIMO} max={Z_MAXIMO} step="1" value={z} onChange={e => setZ(e.target.value)} className={inputBase} />
              <p className="text-[10px] text-zinc-400 mt-1">
                Sem produção por este tempo, a máquina é considerada parada (o início da parada é a última produção).
                Deve ser maior que o intervalo de publicação do WISE.
              </p>
            </div>
          </div>

          <div>
            <div className="flex items-center justify-between mb-1">
              <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">
                Regras de classificação {personalizado ? '(personalizadas para esta máquina)' : '(do catálogo)'}
              </p>
              {carregado && (personalizado
                ? <button onClick={() => setPersonalizado(false)} className={btnSecondarySm}>Voltar às regras do catálogo</button>
                : <button onClick={personalizar} className={btnSecondarySm}>Personalizar para esta máquina</button>)}
            </div>

            {!carregado ? (
              !erro && <p className="text-xs text-zinc-400">Carregando...</p>
            ) : personalizado ? (
              <EditorRegras regras={regras} onChange={setRegras} motivos={motivos} textos={textos} />
            ) : (
              <div className="flex flex-col gap-1">
                <p className="text-[10px] text-zinc-400 mb-1">
                  Esta máquina usa as regras padrão da {item.maquinaNome}, editadas em Configurações &gt; Máquinas &gt; aba Semi Automático.
                </p>
                {paraEdicao(regrasCatalogo).map((r, i) => {
                  const motivo = motivos.find(m => m.id === r.motivoParadaId)
                  return (
                    <p key={r.chave} className={`text-xs ${r.ativa ? 'text-zinc-700 dark:text-zinc-300' : 'text-zinc-400 line-through'}`}>
                      {i + 1}. Se {r.condicoes.map(c => textoCondicao(c, textos)).join(' e ')}
                      {' -> '}{motivo ? `${motivo.nome} (${motivo.tipo.toLowerCase()})` : '—'}
                    </p>
                  )
                })}
                {regrasCatalogo.length === 0 && <p className="text-xs text-zinc-400">Nenhuma regra no catálogo.</p>}
              </div>
            )}
          </div>
        </div>

        <div className={modalFooter}>
          <button onClick={onFechar} disabled={salvando} className={btnSecondarySm}>Cancelar</button>
          <button onClick={salvar} disabled={salvando || !carregado} className={btnPrimary}>
            {salvando ? 'Salvando...' : 'Salvar'}
          </button>
        </div>
      </div>
    </div>
  )
}
