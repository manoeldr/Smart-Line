// Aba "Exportar/Importar" da tela de Configurações — acesso exclusivo do Administrador/Desenvolvedor.
// Exportar: gera um .zip com as categorias selecionadas, baixado direto pelo navegador.
// Importar: envia um .zip, mostra uma prévia (contagem por categoria) antes de aplicar no banco.
import { useState } from 'react'
import { exportImportService, type ExportOpcoes, type ImportResumoDto } from '../../services/exportImportService'
import { btnPrimary, btnSecondarySm } from '../../styles/buttons'
import { checkbox } from '../../styles/inputs'
import { cardPadded } from '../../styles/cards'

export default function AbaExportImport() {
  // ── Exportar ──────────────────────────────────────────────
  const [opcoes, setOpcoes] = useState<ExportOpcoes>({
    clientesLinhas: true,
    maquinas: true,
    sessoesMedicoes: false,
    usuarios: false,
  })
  const [exportando, setExportando] = useState(false)
  const [erroExport, setErroExport] = useState<string | null>(null)

  function toggleOpcao(chave: keyof ExportOpcoes) {
    setOpcoes(prev => ({ ...prev, [chave]: !prev[chave] }))
  }

  async function handleExportar() {
    setExportando(true)
    setErroExport(null)
    try {
      await exportImportService.exportar(opcoes)
    } catch (e: unknown) {
      setErroExport(e instanceof Error ? e.message : 'Erro ao exportar')
    } finally {
      setExportando(false)
    }
  }

  const nenhumaOpcaoMarcada = !opcoes.clientesLinhas && !opcoes.maquinas && !opcoes.sessoesMedicoes && !opcoes.usuarios

  // ── Importar ──────────────────────────────────────────────
  const [arquivo, setArquivo] = useState<File | null>(null)
  const [previa, setPrevia] = useState<ImportResumoDto | null>(null)
  const [carregandoPrevia, setCarregandoPrevia] = useState(false)
  const [importando, setImportando] = useState(false)
  const [resultadoImport, setResultadoImport] = useState<ImportResumoDto | null>(null)
  const [erroImport, setErroImport] = useState<string | null>(null)

  async function handleSelecionarArquivo(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setArquivo(file)
    setPrevia(null)
    setResultadoImport(null)
    setErroImport(null)
    setCarregandoPrevia(true)
    try {
      const resumo = await exportImportService.previa(file)
      setPrevia(resumo)
    } catch (err: unknown) {
      setErroImport(err instanceof Error ? err.message : 'Erro ao ler o arquivo')
    } finally {
      setCarregandoPrevia(false)
    }
  }

  async function handleConfirmarImportacao() {
    if (!arquivo) return
    setImportando(true)
    setErroImport(null)
    try {
      const resultado = await exportImportService.importar(arquivo)
      setResultadoImport(resultado)
      setArquivo(null)
      setPrevia(null)
    } catch (err: unknown) {
      setErroImport(err instanceof Error ? err.message : 'Erro ao importar')
    } finally {
      setImportando(false)
    }
  }

  function cancelarImportacao() {
    setArquivo(null)
    setPrevia(null)
    setErroImport(null)
  }

  return (
    <div className="flex flex-col gap-6 max-w-2xl">

      {/* ── Exportar ────────────────────────────────────────── */}
      <div className={cardPadded}>
        <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100 mb-1">Exportar dados</p>
        <p className="text-xs text-zinc-400 mb-4">
          Gera um arquivo .zip com as categorias selecionadas, para importar em outro computador.
        </p>

        <div className="flex flex-col gap-2 mb-4">
          <label className="flex items-center gap-2 cursor-pointer">
            <input type="checkbox" checked={opcoes.clientesLinhas} onChange={() => toggleOpcao('clientesLinhas')} className={checkbox} />
            <span className="text-xs text-zinc-900 dark:text-zinc-100">Clientes + Linhas <span className="text-zinc-400">(com máquinas associadas)</span></span>
          </label>
          <label className="flex items-center gap-2 cursor-pointer">
            <input type="checkbox" checked={opcoes.maquinas} onChange={() => toggleOpcao('maquinas')} className={checkbox} />
            <span className="text-xs text-zinc-900 dark:text-zinc-100">Máquinas <span className="text-zinc-400">(catálogo, campos de coleta, motivos de parada)</span></span>
          </label>
          <label className="flex items-center gap-2 cursor-pointer">
            <input type="checkbox" checked={opcoes.sessoesMedicoes} onChange={() => toggleOpcao('sessoesMedicoes')} className={checkbox} />
            <span className="text-xs text-zinc-900 dark:text-zinc-100">Sessões / Medições <span className="text-zinc-400">(produção, paradas, leituras)</span></span>
          </label>
          <label className="flex items-center gap-2 cursor-pointer">
            <input type="checkbox" checked={opcoes.usuarios} onChange={() => toggleOpcao('usuarios')} className={checkbox} />
            <span className="text-xs text-zinc-900 dark:text-zinc-100">Usuários <span className="text-zinc-400">(login e senha)</span></span>
          </label>
        </div>

        {erroExport && (
          <p className="text-xs text-red-500 bg-red-50 dark:bg-red-950 px-3 py-2 mb-3">{erroExport}</p>
        )}

        <button onClick={handleExportar} disabled={nenhumaOpcaoMarcada || exportando} className={btnPrimary}>
          {exportando ? 'Gerando arquivo...' : 'Exportar'}
        </button>
      </div>

      {/* ── Importar ────────────────────────────────────────── */}
      <div className={cardPadded}>
        <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100 mb-1">Importar dados</p>
        <p className="text-xs text-zinc-400 mb-4">
          Selecione um arquivo .zip exportado por este sistema. Os dados existentes com o mesmo ID serão atualizados.
        </p>

        {!arquivo && (
          <label className="inline-flex items-center gap-1.5 h-8 px-3 border border-dashed border-zinc-300 dark:border-zinc-700 text-xs text-zinc-500 hover:bg-zinc-50 dark:hover:bg-zinc-800 cursor-pointer transition-colors">
            <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" y1="3" x2="12" y2="15"/></svg>
            Selecionar arquivo .zip
            <input type="file" accept=".zip" onChange={handleSelecionarArquivo} className="hidden" />
          </label>
        )}

        {carregandoPrevia && <p className="text-xs text-zinc-400 mt-3">Lendo arquivo...</p>}

        {erroImport && (
          <p className="text-xs text-red-500 bg-red-50 dark:bg-red-950 px-3 py-2 mt-3">{erroImport}</p>
        )}

        {previa && arquivo && (
          <div className="mt-3">
            <p className="text-xs text-zinc-500 mb-2">Arquivo: <span className="text-zinc-900 dark:text-zinc-100">{arquivo.name}</span></p>
            <div className="grid grid-cols-5 gap-2 mb-4">
              <ResumoItem label="Clientes" valor={previa.clientes} />
              <ResumoItem label="Linhas" valor={previa.linhas} />
              <ResumoItem label="Máquinas" valor={previa.maquinas} />
              <ResumoItem label="Sessões" valor={previa.sessoes} />
              <ResumoItem label="Usuários" valor={previa.usuarios} />
            </div>
            <div className="flex gap-2">
              <button onClick={cancelarImportacao} disabled={importando} className={btnSecondarySm}>
                Cancelar
              </button>
              <button onClick={handleConfirmarImportacao} disabled={importando} className={btnPrimary}>
                {importando ? 'Importando...' : 'Confirmar importação'}
              </button>
            </div>
          </div>
        )}

        {resultadoImport && (
          <div className="mt-3 bg-green-50 dark:bg-green-950 border border-green-200 dark:border-green-800 px-3 py-2">
            <p className="text-xs text-green-700 dark:text-green-400 font-medium mb-1">Importação concluída</p>
            <p className="text-[11px] text-green-700 dark:text-green-400">
              {resultadoImport.clientes} clientes, {resultadoImport.linhas} linhas, {resultadoImport.maquinas} máquinas, {resultadoImport.sessoes} sessões, {resultadoImport.usuarios} usuários
            </p>
          </div>
        )}
      </div>
    </div>
  )
}

function ResumoItem({ label, valor }: { label: string; valor: number }) {
  return (
    <div className="bg-zinc-50 dark:bg-zinc-800 p-2 text-center">
      <p className="text-sm font-medium text-zinc-900 dark:text-zinc-100">{valor}</p>
      <p className="text-[9px] text-zinc-400">{label}</p>
    </div>
  )
}