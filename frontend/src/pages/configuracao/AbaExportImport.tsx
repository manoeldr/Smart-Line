// Aba "Exportar/Importar" da tela de Configurações — acesso exclusivo do Administrador/Desenvolvedor.
// Exportar: gera um .zip com as categorias selecionadas, baixado direto pelo navegador.
// Importar: envia um .zip, mostra uma prévia (contagem por categoria) antes de aplicar no banco.
import { useEffect, useState } from 'react'
import { exportImportService, type ExportOpcoes, type ImportResumoDto } from '../../services/exportImportService'
import { btnPrimary, btnSecondarySm } from '../../styles/buttons'
import Switch from '../../components/Switch'
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
  // Segundos até recarregar o sistema depois de importar (nulo = sem contagem)
  const [recarregaEm, setRecarregaEm] = useState<number | null>(null)

  useEffect(() => {
    if (recarregaEm === null) return
    if (recarregaEm <= 0) { window.location.reload(); return }
    const id = setTimeout(() => setRecarregaEm(s => (s ?? 1) - 1), 1000)
    return () => clearTimeout(id)
  }, [recarregaEm])
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
      // O topo (clientes, linhas) e as outras telas carregaram os dados ao abrir e não sabem da
      // importação: recarregar o sistema é o único jeito de tudo refletir o que entrou. A sessão
      // fica (login guardado) e a tela volta nesta mesma aba. Dá 4 s para ler o resumo.
      setRecarregaEm(4)
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
        <p className="text-sm font-medium text-texto mb-1">Exportar dados</p>
        <p className="text-xs text-texto-3 mb-4">
          Gera um arquivo .zip com as categorias selecionadas, para importar em outro computador.
        </p>

        <div className="flex flex-col gap-2 mb-4">
          <div className="flex items-center gap-2">
            <Switch checked={opcoes.clientesLinhas} onChange={() => toggleOpcao('clientesLinhas')} />
            <span onClick={() => toggleOpcao('clientesLinhas')} className="cursor-pointer text-xs text-texto">Clientes + Linhas <span className="text-texto-3">(máquinas da linha, regras personalizadas, cadastro de WISE)</span></span>
          </div>
          <div className="flex items-center gap-2">
            <Switch checked={opcoes.maquinas} onChange={() => toggleOpcao('maquinas')} />
            <span onClick={() => toggleOpcao('maquinas')} className="cursor-pointer text-xs text-texto">Máquinas <span className="text-texto-3">(catálogo, campos de coleta, motivos de parada, regras e textos das entradas)</span></span>
          </div>
          <div className="flex items-center gap-2">
            <Switch checked={opcoes.sessoesMedicoes} onChange={() => toggleOpcao('sessoesMedicoes')} />
            <span onClick={() => toggleOpcao('sessoesMedicoes')} className="cursor-pointer text-xs text-texto">Sessões / Medições <span className="text-texto-3">(produção, paradas, leituras, trechos sem comunicação)</span></span>
          </div>
          <div className="flex items-center gap-2">
            <Switch checked={opcoes.usuarios} onChange={() => toggleOpcao('usuarios')} />
            <span onClick={() => toggleOpcao('usuarios')} className="cursor-pointer text-xs text-texto">Usuários <span className="text-texto-3">(login e senha)</span></span>
          </div>
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
        <p className="text-sm font-medium text-texto mb-1">Importar dados</p>
        <p className="text-xs text-texto-3 mb-4">
          Selecione um arquivo .zip exportado por este sistema. Os dados existentes com o mesmo ID serão atualizados.
        </p>

        {!arquivo && (
          <label className="inline-flex items-center gap-1.5 h-8 px-3 border border-dashed border-borda-forte text-xs text-texto-2 hover:bg-superficie-2 cursor-pointer transition-colors">
            <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" y1="3" x2="12" y2="15"/></svg>
            Selecionar arquivo .zip
            <input type="file" accept=".zip" onChange={handleSelecionarArquivo} className="hidden" />
          </label>
        )}

        {carregandoPrevia && <p className="text-xs text-texto-3 mt-3">Lendo arquivo...</p>}

        {erroImport && (
          <p className="text-xs text-red-500 bg-red-50 dark:bg-red-950 px-3 py-2 mt-3">{erroImport}</p>
        )}

        {previa && arquivo && (
          <div className="mt-3">
            <p className="text-xs text-texto-2 mb-2">Arquivo: <span className="text-texto">{arquivo.name}</span></p>
            <div className="grid grid-cols-8 gap-2 mb-4">
              <ResumoItem label="Clientes" valor={previa.clientes} />
              <ResumoItem label="Linhas" valor={previa.linhas} />
              <ResumoItem label="Máquinas" valor={previa.maquinas} />
              <ResumoItem label="Regras" valor={previa.regras} />
              <ResumoItem label="Textos" valor={previa.textosEntradas} />
              <ResumoItem label="WISE" valor={previa.wises} />
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
              {resultadoImport.clientes} clientes, {resultadoImport.linhas} linhas, {resultadoImport.maquinas} máquinas, {resultadoImport.regras} regras, {resultadoImport.textosEntradas} textos de entradas, {resultadoImport.wises} WISE, {resultadoImport.sessoes} sessões, {resultadoImport.usuarios} usuários
            </p>
            {recarregaEm !== null && (
              <p className="text-[11px] text-green-700 dark:text-green-400 mt-1.5 flex items-center gap-2">
                <span>As telas serão atualizadas em {recarregaEm} s para mostrar o que foi importado.</span>
                <button onClick={() => window.location.reload()} className="underline">Atualizar agora</button>
              </p>
            )}
          </div>
        )}
      </div>
    </div>
  )
}

function ResumoItem({ label, valor }: { label: string; valor: number }) {
  return (
    <div className="bg-superficie-2 p-2 text-center">
      <p className="text-sm font-medium text-texto">{valor}</p>
      <p className="text-[9px] text-texto-3">{label}</p>
    </div>
  )
}