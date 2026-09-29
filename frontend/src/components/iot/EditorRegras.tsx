// Editor das regras de classificação de paradas (Semi Automático). Controlado: quem usa guarda
// a lista e decide quando salvar. A ordem é a prioridade (arrastar para reordenar): a primeira
// regra com todas as condições verdadeiras dá o motivo. Usado no catálogo (aba Semi Automático
// da máquina) e na personalização por máquina da linha.
import {
  DndContext, closestCenter, PointerSensor, useSensor, useSensors, type DragEndEvent,
} from '@dnd-kit/core'
import { SortableContext, verticalListSortingStrategy, useSortable, arrayMove } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import type { TipoCondicao } from '../../services/regrasClassificacaoService'
import type { TextoEntrada } from '../../utils/entradasWise'
import { SENSORES_WISE } from '../../utils/entradasWise'
import { chaveNova, textoCondicao, type CondicaoEdit, type MotivoOpcao, type RegraEdit } from './regrasEdicao'
import Switch from '../Switch'
import { btnIconDanger, btnPrimaryXs } from '../../styles/buttons'
import { inputBase } from '../../styles/inputs'

interface Props {
  regras: RegraEdit[]
  onChange: (regras: RegraEdit[]) => void
  motivos: MotivoOpcao[]
  // Textos das entradas (os que estão sendo editados, para os rótulos acompanharem)
  textos: Record<string, TextoEntrada>
}

const TIPOS: { valor: TipoCondicao; texto: string }[] = [
  { valor: 'SensorEmAlarme', texto: 'Sensor ativo' },
  { valor: 'SensorForaDeAlarme', texto: 'Sensor normal' },
  { valor: 'TempoParadaMinimo', texto: 'Parada há pelo menos' },
]

export default function EditorRegras({ regras, onChange, motivos, textos }: Props) {
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 5 } }))

  function alterar(chave: string, mudanca: Partial<RegraEdit>) {
    onChange(regras.map(r => r.chave === chave ? { ...r, ...mudanca } : r))
  }

  function remover(chave: string) {
    onChange(regras.filter(r => r.chave !== chave))
  }

  function adicionar() {
    onChange([...regras, {
      chave: chaveNova(), id: null, nome: '', motivoParadaId: '', ativa: true,
      condicoes: [{ tipo: 'SensorEmAlarme', canal: 'S8', minutos: '' }],
    }])
  }

  function aoArrastar(e: DragEndEvent) {
    if (!e.over || e.active.id === e.over.id) return
    const de = regras.findIndex(r => r.chave === e.active.id)
    const para = regras.findIndex(r => r.chave === e.over!.id)
    onChange(arrayMove(regras, de, para))
  }

  return (
    <div>
      {regras.length === 0 && (
        <p className="text-xs text-zinc-400 py-2">Nenhuma regra: todas as paradas ficarão sem motivo até alguém classificar.</p>
      )}
      <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={aoArrastar}>
        <SortableContext items={regras.map(r => r.chave)} strategy={verticalListSortingStrategy}>
          {regras.map((r, i) => (
            <RegraItem
              key={r.chave}
              regra={r}
              posicao={i + 1}
              motivos={motivos}
              textos={textos}
              onAlterar={m => alterar(r.chave, m)}
              onRemover={() => remover(r.chave)}
            />
          ))}
        </SortableContext>
      </DndContext>
      <button onClick={adicionar} className={`${btnPrimaryXs} mt-2 flex items-center gap-1`}>
        <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
        Nova regra
      </button>
    </div>
  )
}

interface ItemProps {
  regra: RegraEdit
  posicao: number
  motivos: MotivoOpcao[]
  textos: Record<string, TextoEntrada>
  onAlterar: (mudanca: Partial<RegraEdit>) => void
  onRemover: () => void
}

function RegraItem({ regra, posicao, motivos, textos, onAlterar, onRemover }: ItemProps) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: regra.chave })
  const style = { transform: CSS.Transform.toString(transform), transition, opacity: isDragging ? 0.5 : 1 }

  const motivo = motivos.find(m => m.id === regra.motivoParadaId)

  function alterarCondicao(indice: number, mudanca: Partial<CondicaoEdit>) {
    onAlterar({ condicoes: regra.condicoes.map((c, i) => i === indice ? { ...c, ...mudanca } : c) })
  }

  return (
    <div
      ref={setNodeRef}
      style={style}
      className={`border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 mb-2 ${regra.ativa ? '' : 'opacity-60'}`}
    >
      {/* Cabeçalho: ordem, motivo, nome, ativa */}
      <div className="flex items-center gap-2 px-2 py-1.5 border-b border-zinc-100 dark:border-zinc-800">
        <button {...attributes} {...listeners} title="Arraste para mudar a prioridade" className="cursor-grab text-zinc-400 hover:text-zinc-600 dark:hover:text-zinc-300 touch-none">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <line x1="8" y1="6" x2="16" y2="6"/><line x1="8" y1="12" x2="16" y2="12"/><line x1="8" y1="18" x2="16" y2="18"/>
          </svg>
        </button>
        <span className="text-[10px] text-zinc-400 w-4 text-right">{posicao}</span>
        <select
          value={regra.motivoParadaId}
          onChange={e => onAlterar({ motivoParadaId: e.target.value })}
          className={`${inputBase} flex-1`}
        >
          <option value="">Motivo...</option>
          {motivos.filter(m => m.ativo || m.id === regra.motivoParadaId).map(m => (
            <option key={m.id} value={m.id}>
              {m.nome} ({m.tipo.toLowerCase()}){m.ativo ? '' : ' - inativo'}
            </option>
          ))}
        </select>
        <input
          value={regra.nome}
          onChange={e => onAlterar({ nome: e.target.value })}
          placeholder="Nome da regra (opcional)"
          className={`${inputBase} flex-1`}
        />
        <Switch checked={regra.ativa} onChange={v => onAlterar({ ativa: v })} />
        <button onClick={onRemover} title="Remover regra" className={btnIconDanger}>
          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/></svg>
        </button>
      </div>

      {/* Condições */}
      <div className="px-2 py-1.5 pl-9 flex flex-col gap-1.5">
        {regra.condicoes.map((c, i) => (
          <div key={i} className="flex items-center gap-2">
            <span className="text-[10px] text-zinc-400 w-5">{i === 0 ? 'Se' : 'e'}</span>
            <select
              value={c.tipo}
              onChange={e => alterarCondicao(i, { tipo: e.target.value as TipoCondicao })}
              className={`${inputBase} w-40`}
            >
              {TIPOS.map(t => <option key={t.valor} value={t.valor}>{t.texto}</option>)}
            </select>
            {c.tipo === 'TempoParadaMinimo' ? (
              <div className="flex items-center gap-1.5 flex-1">
                <input
                  type="number" min="1" step="1"
                  value={c.minutos}
                  onChange={e => alterarCondicao(i, { minutos: e.target.value })}
                  className={`${inputBase} w-20`}
                />
                <span className="text-[10px] text-zinc-400">minutos</span>
              </div>
            ) : (
              <select value={c.canal} onChange={e => alterarCondicao(i, { canal: e.target.value })} className={`${inputBase} flex-1`}>
                {SENSORES_WISE.map(s => (
                  <option key={s} value={s}>{textoCondicao({ ...c, canal: s }, textos)}</option>
                ))}
              </select>
            )}
            <button
              onClick={() => onAlterar({ condicoes: regra.condicoes.filter((_, j) => j !== i) })}
              title="Remover condição"
              className={btnIconDanger}
            >
              <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
            </button>
          </div>
        ))}
        <button
          onClick={() => onAlterar({ condicoes: [...regra.condicoes, { tipo: 'SensorEmAlarme', canal: 'S8', minutos: '' }] })}
          className="self-start text-[10px] text-blue-600 dark:text-blue-400 hover:underline"
        >
          + condição
        </button>

        {/* Resumo em texto corrido */}
        {regra.condicoes.length > 0 && (
          <p className="text-[10px] text-zinc-500">
            Se {regra.condicoes.map(c => textoCondicao(c, textos)).join(' e ')}
            {' -> '}{motivo ? `${motivo.nome} (${motivo.tipo.toLowerCase()})` : 'motivo não escolhido'}
          </p>
        )}
      </div>
    </div>
  )
}
