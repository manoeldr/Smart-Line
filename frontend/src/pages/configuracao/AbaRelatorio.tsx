// Aba "Relatório" das Configurações — a folha timbrada usada no relatório para o cliente.
// Importa o arquivo (PDF ou imagem), mostra a prévia com a área útil marcada e deixa ajustar
// as margens (o que fica livre do timbre em cada borda). Administrador e Desenvolvedor; remover, só o Desenvolvedor.
import { useRef, useState } from 'react'
import { modeloRelatorioService, type MargensRelatorio } from '../../services/modeloRelatorioService'
import { useModeloRelatorio } from '../../hooks/useModeloRelatorio'
import { btnPrimary, btnSecondary, btnDanger } from '../../styles/buttons'
import { inputMd, label } from '../../styles/inputs'
import { cardPadded } from '../../styles/cards'
import { mensagemErro } from '../../services/api'
import ConfirmModal from '../../components/ConfirmModal'
import { dataHora } from '../../utils/tempo'
import { useAuth } from '../../contexts/AuthContext'

const CAMPOS_MARGEM: { chave: keyof MargensRelatorio; rotulo: string }[] = [
  { chave: 'superior', rotulo: 'Superior' },
  { chave: 'inferior', rotulo: 'Inferior' },
  { chave: 'esquerda', rotulo: 'Esquerda' },
  { chave: 'direita', rotulo: 'Direita' },
]

export default function AbaRelatorio() {
  const { modelo, setModelo, imagemUrl, erro: erroCarga } = useModeloRelatorio()
  // Remover a folha: só o Desenvolvedor (o Administrador importa e ajusta as margens)
  const desenvolvedor = useAuth().usuario?.nivel === 'Desenvolvedor'
  const inputRef = useRef<HTMLInputElement>(null)

  const [importando, setImportando] = useState(false)
  const [salvando, setSalvando] = useState(false)
  const [confirmarRemocao, setConfirmarRemocao] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  const [aviso, setAviso] = useState<string | null>(null)

  // Margens em edição (texto, para aceitar vírgula). Sem edição, mostra as do servidor.
  type MargensTexto = Record<keyof MargensRelatorio, string>
  const [editadas, setEditadas] = useState<MargensTexto | null>(null)
  const doServidor: MargensTexto = {
    superior: String(modelo?.margens.superior ?? ''),
    inferior: String(modelo?.margens.inferior ?? ''),
    esquerda: String(modelo?.margens.esquerda ?? ''),
    direita: String(modelo?.margens.direita ?? ''),
  }
  const margens = editadas ?? doServidor

  function margensNumericas(): MargensRelatorio | null {
    const r = {} as MargensRelatorio
    for (const c of CAMPOS_MARGEM) {
      const n = Number(margens[c.chave].replace(',', '.'))
      if (!Number.isFinite(n) || n < 0) return null
      r[c.chave] = n
    }
    return r
  }
  const margensValidas = margensNumericas()
  const margensMudaram = !!modelo && !!margensValidas && CAMPOS_MARGEM.some(c => margensValidas[c.chave] !== modelo.margens[c.chave])

  async function handleArquivo(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (!file) return
    setImportando(true)
    setErro(null)
    setAviso(null)
    try {
      setModelo(await modeloRelatorioService.importar(file))
      setAviso('Folha importada.')
    } catch (err: unknown) {
      setErro(mensagemErro(err, 'Não foi possível importar a folha.'))
    } finally {
      setImportando(false)
    }
  }

  async function handleSalvarMargens() {
    if (!margensValidas) return
    setSalvando(true)
    setErro(null)
    setAviso(null)
    try {
      setModelo(await modeloRelatorioService.atualizarMargens(margensValidas))
      setEditadas(null)
    } catch (err: unknown) {
      setErro(mensagemErro(err, 'Não foi possível salvar as margens.'))
    } finally {
      setSalvando(false)
    }
  }

  async function handleRemover() {
    setConfirmarRemocao(false)
    setErro(null)
    setAviso(null)
    try {
      await modeloRelatorioService.remover()
      setModelo(await modeloRelatorioService.obter())
      setAviso('Folha removida.')
    } catch (err: unknown) {
      setErro(mensagemErro(err, 'Não foi possível remover a folha.'))
    }
  }

  // Prévia em escala: A4 (210 × 297 mm) numa caixa de 300 px de largura
  const escala = 300 / 210
  const m = margensValidas ?? modelo?.margens

  return (
    <div className="flex flex-col gap-4 max-w-4xl">
      <div className={`${cardPadded} flex flex-col gap-3`}>
        <div>
          <p className="text-sm font-medium text-texto">Folha timbrada do relatório</p>
          <p className="text-xs text-texto-2 mt-0.5">Importar em PDF.</p>
        </div>

        <div className="flex items-center gap-3 flex-wrap">
          <input ref={inputRef} type="file" accept=".pdf,.png,.jpg,.jpeg,application/pdf,image/png,image/jpeg" onChange={handleArquivo} className="hidden" />
          <button onClick={() => inputRef.current?.click()} disabled={importando} className={btnPrimary}>
            {importando ? 'Importando...' : modelo?.existe ? 'Substituir folha' : 'Importar folha'}
          </button>
          {modelo?.existe && desenvolvedor && (
            <button onClick={() => setConfirmarRemocao(true)} className={btnDanger}>Remover</button>
          )}
          {modelo?.existe && (
            <p className="text-xs text-texto-2">
              {modelo.nomeArquivo ?? 'folha'} · importada em {dataHora(modelo.importadoEm)}
            </p>
          )}
          {modelo && !modelo.existe && <p className="text-xs text-texto-3">Nenhuma folha importada.</p>}
        </div>

        {(erro || erroCarga) && <p className="text-xs text-red-500">{erro ?? erroCarga}</p>}
        {aviso && <p className="text-xs text-green-700 dark:text-green-400">{aviso}</p>}
      </div>

      {modelo?.existe && (
        <div className={`${cardPadded} flex gap-6 flex-wrap`}>
          {/* Prévia */}
          <div className="flex flex-col gap-2">
            <p className={label}>Prévia · área útil em azul</p>
            <div className="relative bg-white shadow border border-borda overflow-hidden" style={{ width: 300, height: 297 * escala }}>
              {imagemUrl && <img src={imagemUrl} alt="Folha timbrada" className="absolute inset-0 w-full h-full" />}
              {m && (
                <div
                  className="absolute border border-dashed border-blue-500 bg-blue-500/10"
                  style={{
                    top: m.superior * escala,
                    bottom: m.inferior * escala,
                    left: m.esquerda * escala,
                    right: m.direita * escala,
                  }}
                />
              )}
            </div>
          </div>

          {/* Margens */}
          <div className="flex flex-col gap-3 flex-1 min-w-56">
            <div>
              <p className="text-sm font-medium text-texto">Margens</p>
              <p className="text-xs text-texto-2 mt-0.5">Área livre do timbre em cada borda.</p>
            </div>
            <div className="grid grid-cols-2 gap-3">
              {CAMPOS_MARGEM.map(c => (
                <div key={c.chave} className="flex flex-col gap-1">
                  <label className={label}>{c.rotulo} (mm)</label>
                  <input
                    type="text"
                    inputMode="decimal"
                    value={margens[c.chave]}
                    onChange={e => setEditadas({ ...margens, [c.chave]: e.target.value })}
                    className={`${inputMd} w-full`}
                  />
                </div>
              ))}
            </div>
            <div className="flex gap-2">
              <button onClick={handleSalvarMargens} disabled={!margensValidas || !margensMudaram || salvando} className={btnPrimary}>
                {salvando ? 'Salvando...' : 'Salvar margens'}
              </button>
              {margensMudaram && (
                <button onClick={() => setEditadas(null)} className={btnSecondary}>
                  Desfazer
                </button>
              )}
            </div>
          </div>
        </div>
      )}

      <ConfirmModal
        open={confirmarRemocao}
        titulo="Remover a folha timbrada?"
        mensagem="O relatório volta a sair sem timbre."
        onConfirmar={handleRemover}
        onCancelar={() => setConfirmarRemocao(false)}
      />
    </div>
  )
}
