// Tela de Relatórios — escolhe o recorte (por enquanto: uma linha, um dia), preenche o que é
// digitado, confere a folha A4 na tela e exporta em PDF. A exportação é a impressão da própria
// folha (Ctrl+P / "Salvar como PDF"); o resto da tela some na impressão (ver index.css).
// Produto e Responsável do Cliente são obrigatórios para exportar.
import { Fragment, useEffect, useMemo, useRef, useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { useAuth } from '../../contexts/AuthContext'
import { linhaService } from '../../services/linhaService'
import { clienteService } from '../../services/clienteService'
import { sessaoDetalheService, type SessaoDetalheDto } from '../../services/sessaoDetalheService'
import type { Linha } from '../../types'
import CampoComLista from '../../components/CampoComLista'
import { inputMd, label } from '../../styles/inputs'
import { btnPrimary } from '../../styles/buttons'
import { cardPadded } from '../../styles/cards'
import { maquinaDeReferencia } from './referencia'
import { lerLista, lembrarNaLista, removerDaLista } from './listasRelatorio'
import SecaoDadosMedicao, { type DadosMedicao } from './SecaoDadosMedicao'
import SecaoResumoGeral from './SecaoResumoGeral'
import SecaoIndicadores from './SecaoIndicadores'
import PaginaMaquina from './PaginaMaquina'
import { partesDaMaquina } from './partesMaquina'
import LinhaDoTempo from './LinhaDoTempo'
import { LINHAS_POR_FOLHA, LINHAS_APOS_GRAFICO, fatiarEventos, eventosCronologicos } from './linhaDoTempoFolhas'
import { BLOCOS, type Bloco, lerBlocos, guardarBlocos } from './blocos'
import Switch from '../../components/Switch'
import EnviarRelatorioEmailModal from '../../modals/EnviarRelatorioEmailModal'
import { btnSecondary } from '../../styles/buttons'
import { relatorioService } from '../../services/relatorioService'
import Logo from '../../components/Logo'
import { useModeloRelatorio } from '../../hooks/useModeloRelatorio'
import type { MargensRelatorio } from '../../services/modeloRelatorioService'

// Uma folha A4: com timbre, a imagem no fundo e as margens configuradas; sem timbre, o padrão.
function Pagina({ comTimbre, timbreUrl, margens, children }: {
  comTimbre: boolean
  timbreUrl: string | null
  margens?: MargensRelatorio
  children: React.ReactNode
}) {
  return (
    <div
      className="relatorio-folha bg-white text-zinc-900 shadow-md"
      style={comTimbre && margens ? {
        padding: `${margens.superior}mm ${margens.direita}mm ${margens.inferior}mm ${margens.esquerda}mm`,
      } : undefined}
    >
      {comTimbre && timbreUrl && <img src={timbreUrl} alt="" className="relatorio-timbre" />}
      {children}
    </div>
  )
}

// Nome do PDF: sem caracteres que o Windows não aceita em arquivo; data em dd-mm-aaaa
function nomeArquivoPdf(cliente: string, linha: string, data: string): string {
  const limpar = (t: string) => t.trim().replace(/[\\/:*?"<>|]+/g, '-').replace(/\s+/g, ' ')
  const [ano, mes, dia] = data.split('-')
  return `SmartLineScan_${limpar(cliente)}_${limpar(linha)}_${dia}-${mes}-${ano}`
}

// Ponte do desktop (WebView2): existe só quando a página roda dentro do SmartLine.Desktop
declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage: (mensagem: unknown) => void
        addEventListener: (tipo: 'message', ouvinte: (e: { data: unknown }) => void) => void
        removeEventListener: (tipo: 'message', ouvinte: (e: { data: unknown }) => void) => void
      }
    }
  }
}

function noDesktop(): boolean {
  return typeof window !== 'undefined' && !!window.chrome?.webview
}

function hojeTexto() {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

// Parâmetros pela URL: o servidor abre esta tela assim para gerar o PDF (?impressao=1), e
// também servem para abrir um recorte direto (linha, data, produto, responsavel).
function parametrosUrl() {
  const q = new URLSearchParams(window.location.search)
  return {
    linha: q.get('linha') ?? '',
    data: q.get('data') ?? '',
    produto: q.get('produto') ?? '',
    responsavel: q.get('responsavel') ?? '',
    impressao: q.get('impressao') === '1',
    blocos: q.get('blocos'),
  }
}

export default function Relatorios() {
  const { usuario, clienteId } = useAuth()
  const [urlInicial] = useState(parametrosUrl)
  // Os campos vindos da URL só valem no primeiro cliente carregado; trocar de cliente limpa
  const primeiroClienteRef = useRef(true)
  const modoImpressao = urlInicial.impressao
  // Blocos ligados (todas as máquinas); da URL quando o servidor gera o PDF, senão lembrados
  const [blocos, setBlocos] = useState<Bloco[]>(() => lerBlocos(urlInicial.blocos))
  const [conteudoAberto, setConteudoAberto] = useState(false)
  const conteudoRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!conteudoAberto) return
    function clique(e: MouseEvent) {
      if (conteudoRef.current && !conteudoRef.current.contains(e.target as Node)) setConteudoAberto(false)
    }
    document.addEventListener('mousedown', clique)
    return () => document.removeEventListener('mousedown', clique)
  }, [conteudoAberto])

  function alternarBloco(b: Bloco) {
    setBlocos(atual => {
      const novo = atual.includes(b) ? atual.filter(x => x !== b) : [...atual, b]
      guardarBlocos(novo)
      return novo
    })
  }
  // Folha timbrada (Configurações → Relatório): fundo de cada página e margens do conteúdo
  const { modelo, imagemUrl: timbreUrl } = useModeloRelatorio()
  const comTimbre = !!modelo?.existe && !!timbreUrl
  const [clienteNome, setClienteNome] = useState('')
  const [linhas, setLinhas] = useState<Linha[]>([])
  const [linhaId, setLinhaId] = useState(urlInicial.linha)
  // O dia vem do filtro de data do topo (nulo = hoje). Pela URL (?data=) só na impressão pelo
  // servidor, que abre a tela sem ninguém mexer no filtro; fora dela, ?data= vai para o filtro.
  const { dataFiltro, setDataFiltro } = useOutletContext<{ dataFiltro: string | null; setDataFiltro: (d: string | null) => void }>()
  const dataUrl = /^\d{4}-\d{2}-\d{2}$/.test(urlInicial.data) ? urlInicial.data : null
  const data = modoImpressao && dataUrl ? dataUrl : (dataFiltro ?? hojeTexto())
  useEffect(() => {
    if (!modoImpressao && dataUrl) setDataFiltro(dataUrl === hojeTexto() ? null : dataUrl)
    // só na abertura da tela
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // A linha com as sessões do dia escolhido, e o detalhe da sessão da máquina de referência
  const [linhaDoDia, setLinhaDoDia] = useState<Linha | null>(null)
  const [sessao, setSessao] = useState<SessaoDetalheDto | null>(null)
  // Sessão do dia de cada máquina medida, na ordem da linha (páginas 2 em diante)
  const [sessoesMaquinas, setSessoesMaquinas] = useState<{ sessao: SessaoDetalheDto; critica: boolean }[]>([])
  const [carregando, setCarregando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  // Instante da emissão (capa) e referência do "até agora" de uma medição em andamento
  const [emitidoEm, setEmitidoEm] = useState(() => new Date())
  // Desktop: caminho do último PDF gravado e situação do e-mail (aviso embaixo do botão)
  const [pdfSalvoEm, setPdfSalvoEm] = useState<string | null>(null)
  const [avisoEmail, setAvisoEmail] = useState<{ ok: boolean; texto: string } | null>(null)

  // Desktop: resposta da janela depois de gravar o PDF
  useEffect(() => {
    const webview = window.chrome?.webview
    if (!webview) return
    function aoReceber(e: { data: unknown }) {
      const msg = e.data as { tipo?: string; caminho?: string; mensagem?: string } | null
      if (msg?.tipo === 'pdfExportado' && msg.caminho) setPdfSalvoEm(msg.caminho)
      if (msg?.tipo === 'emailAberto') setAvisoEmail({ ok: true, texto: 'E-mail aberto no seu programa de e-mail com o PDF anexado.' })
      if (msg?.tipo === 'emailErro') setAvisoEmail({ ok: false, texto: msg.mensagem ?? 'Não foi possível abrir o e-mail.' })
      if (msg?.tipo === 'emailSemAnexo') setAvisoEmail({ ok: true, texto: msg.mensagem ?? 'Mensagem aberta sem o anexo: arraste o PDF para ela.' })
    }
    webview.addEventListener('message', aoReceber)
    return () => webview.removeEventListener('message', aoReceber)
  }, [])

  // Campos digitados
  const [produto, setProduto] = useState(urlInicial.produto)
  const [responsavelCliente, setResponsavelCliente] = useState(urlInicial.responsavel)
  const [produtos, setProdutos] = useState<string[]>([])
  const [responsaveis, setResponsaveis] = useState<string[]>([])
  const [emails, setEmails] = useState<string[]>([])
  const [emailAberto, setEmailAberto] = useState(false)
  const [exportando, setExportando] = useState(false)
  // "Pronto": a folha terminou de desenhar (o servidor espera por isto para imprimir)
  const [pronto, setPronto] = useState(false)

  // Nome do cliente e as linhas dele
  useEffect(() => {
    if (!clienteId) return
    const id = clienteId
    async function carregar() {
      // Campos digitados e listas são do cliente: trocou de cliente, recomeça
      setProdutos(lerLista(id, 'produtos'))
      setResponsaveis(lerLista(id, 'responsaveisCliente'))
      setEmails(lerLista(id, 'emailsCliente'))
      if (!primeiroClienteRef.current) {
        setProduto('')
        setResponsavelCliente('')
      }
      primeiroClienteRef.current = false
      const [cliente, ls] = await Promise.all([
        clienteService.getById(id).catch(() => null),
        linhaService.getLinhasByCliente(id),
      ])
      setClienteNome(cliente?.nome ?? '')
      setLinhas(ls)
      setLinhaId(atual => (ls.some(l => l.id === atual) ? atual : ls[0]?.id ?? ''))
    }
    carregar()
  }, [clienteId])

  // Sessões do dia na linha → máquina de referência → detalhe da sessão dela
  useEffect(() => {
    if (!clienteId || !linhaId) return
    const id = clienteId
    let ativo = true
    async function carregar() {
      setPronto(false)
      setCarregando(true)
      setErro(null)
      try {
        const ls = await linhaService.getLinhasByCliente(id, data)
        const linha = ls.find(l => l.id === linhaId) ?? null
        const ref = maquinaDeReferencia(linha)
        // Máquinas medidas no dia, na ordem da linha; a de referência sai da mesma lista
        const medidas = (linha?.maquinas ?? [])
          .filter(m => m.ativo && m.sessaoDia)
          .sort((a, b) => a.ordem - b.ordem)
        const detalhes = await Promise.all(medidas.map(m => sessaoDetalheService.getSessaoDetalhe(m.sessaoDia!.sessaoId)))
        if (!ativo) return
        setLinhaDoDia(linha)
        const porMaquina = medidas.map((m, i) => ({ sessao: detalhes[i], critica: m.critica }))
        setSessoesMaquinas(porMaquina)
        setSessao(ref ? porMaquina.find(x => x.sessao.sessaoId === ref.sessaoDia!.sessaoId)?.sessao ?? null : null)
        setEmitidoEm(new Date())
      } catch (e: unknown) {
        if (ativo) setErro(e instanceof Error ? e.message : 'Erro ao carregar a medição')
      } finally {
        if (ativo) setCarregando(false)
      }
    }
    carregar()
    return () => { ativo = false }
  }, [clienteId, linhaId, data])

  // Marca a folha como pronta um quadro depois de desenhar (gráficos sem animação já estão lá)
  useEffect(() => {
    if (carregando || erro || !linhaDoDia) return
    const id = requestAnimationFrame(() => setPronto(true))
    return () => cancelAnimationFrame(id)
  }, [carregando, erro, linhaDoDia, sessoesMaquinas])

  const referencia = useMemo(() => maquinaDeReferencia(linhaDoDia), [linhaDoDia])
  const temMedicao = !!sessao

  const dados: DadosMedicao = {
    clienteNome,
    linhaNome: linhas.find(l => l.id === linhaId)?.nome ?? '',
    produto: produto.trim(),
    velocidadeNominal: sessao?.velocidadeNominal ?? referencia?.velocidadeNominal ?? null,
    responsavelMedicao: usuario?.nome ?? '',
    responsavelCliente: responsavelCliente.trim(),
    inicio: sessao?.inicio ?? null,
    fim: sessao?.fim ?? null,
    emAndamento: !!sessao && !sessao.fim,
  }

  const faltando: string[] = []
  if (!dados.produto) faltando.push('Produto')
  if (!dados.responsavelCliente) faltando.push('Responsável do Cliente')
  const podeExportar = temMedicao && faltando.length === 0 && !carregando

  function exportar() {
    if (!clienteId || !podeExportar) return
    setProdutos(lembrarNaLista(clienteId, 'produtos', dados.produto))
    setResponsaveis(lembrarNaLista(clienteId, 'responsaveisCliente', dados.responsavelCliente))
    const nome = nomeArquivoPdf(dados.clienteNome, dados.linhaNome, data)
    setPdfSalvoEm(null)
    setAvisoEmail(null)

    // No desktop (WebView2) a janela grava o PDF direto: só o "Salvar como" do Windows,
    // sem a caixa de impressão. A resposta chega pela mensagem "pdfExportado" (ver useEffect).
    if (noDesktop()) {
      window.chrome!.webview!.postMessage({ tipo: 'exportarPdf', nome })
      return
    }

    // No navegador: o servidor gera o PDF e o navegador só baixa. Se o servidor não conseguir
    // (sem Edge/Chrome na máquina), cai na impressão do navegador, com o nome já preenchido.
    setExportando(true)
    relatorioService.baixarPdf(parametros())
      .catch(() => {
        const tituloAnterior = document.title
        document.title = nome
        try { window.print() } finally { document.title = tituloAnterior }
      })
      .finally(() => setExportando(false))
  }

  function parametros() {
    return { linhaId, data, produto: dados.produto, responsavelCliente: dados.responsavelCliente, blocos: blocos.join(',') }
  }

  // Lembra os campos digitados (o mesmo que o Exportar faz) e manda o desktop abrir a mensagem
  // no programa de e-mail do computador (Outlook), já com destinatário, texto e o PDF anexado.
  function enviarEmail(email: { para: string; assunto: string; mensagem: string }) {
    if (!clienteId || !podeExportar) return
    setEmailAberto(false)
    setAvisoEmail(null)
    setProdutos(lembrarNaLista(clienteId, 'produtos', dados.produto))
    setResponsaveis(lembrarNaLista(clienteId, 'responsaveisCliente', dados.responsavelCliente))
    setEmails(lembrarNaLista(clienteId, 'emailsCliente', email.para))
    window.chrome!.webview!.postMessage({
      tipo: 'enviarEmail',
      nome: nomeArquivoPdf(dados.clienteNome, dados.linhaNome, data),
      ...email,
    })
  }

  const dataTexto = new Date(data + 'T00:00:00').toLocaleDateString('pt-BR')
  const assuntoEmail = `Relatório de medição – ${dados.linhaNome} – ${dataTexto}`
  const mensagemEmail =
    `Prezado(a) ${dados.responsavelCliente || 'responsável'},\n\n` +
    `Segue em anexo o relatório de medição da linha ${dados.linhaNome} (${dados.clienteNome}), referente a ${dataTexto}` +
    `${dados.produto ? `, produto ${dados.produto}` : ''}.\n\n` +
    `Qualquer dúvida, fico à disposição.\n\n` +
    `${dados.responsavelMedicao}\nSanmartin · Smart Line Scan`

  return (
    <div className="flex flex-col">

      {/* Recorte e campos digitados — fixos no alto enquanto a folha rola; somem na impressão */}
      <div className={`relatorio-controles sticky top-0 z-30 bg-fundo px-4 pt-4 pb-3 ${modoImpressao ? 'hidden' : ''}`}>
      <div className={`${cardPadded} flex flex-col gap-3`}>
        <div className="flex items-end gap-3 flex-wrap">
          <div className="flex flex-col gap-1.5">
            <label className={label}>Linha</label>
            <select value={linhaId} onChange={e => setLinhaId(e.target.value)} className={inputMd}>
              {linhas.map(l => <option key={l.id} value={l.id}>{l.nome}</option>)}
            </select>
          </div>
          <div className="flex flex-col gap-1.5 w-64">
            <label className={label}>Produto *</label>
            <CampoComLista
              valor={produto}
              onChange={setProduto}
              sugestoes={produtos}
              onRemoverSugestao={v => clienteId && setProdutos(removerDaLista(clienteId, 'produtos', v))}
              placeholder="Ex.: Água mineral 500 ml"
              obrigatorio
            />
          </div>
          <div className="flex flex-col gap-1.5 w-72">
            <label className={label}>Responsável do Cliente *</label>
            <CampoComLista
              valor={responsavelCliente}
              onChange={setResponsavelCliente}
              sugestoes={responsaveis}
              onRemoverSugestao={v => clienteId && setResponsaveis(removerDaLista(clienteId, 'responsaveisCliente', v))}
              placeholder="Nome completo"
              obrigatorio
            />
          </div>
          <div ref={conteudoRef} className="relative flex flex-col gap-1.5">
            <label className={label}>Conteúdo</label>
            <button onClick={() => setConteudoAberto(a => !a)} className={`${btnSecondary} h-9`}>
              {blocos.length === BLOCOS.length ? 'Tudo' : `${blocos.length} de ${BLOCOS.length}`} ▾
            </button>
            {conteudoAberto && (
              <div className="absolute top-full left-0 mt-1 z-40 bg-superficie border border-borda-forte shadow-lg p-3 flex flex-col gap-2 w-56">
                <p className="text-[10px] text-texto-3">Nas páginas de cada máquina</p>
                {BLOCOS.map(b => (
                  <label key={b.id} className="flex items-center justify-between gap-3 text-xs text-texto cursor-pointer">
                    {b.rotulo}
                    <Switch checked={blocos.includes(b.id)} onChange={() => alternarBloco(b.id)} />
                  </label>
                ))}
              </div>
            )}
          </div>
          <div className="ml-auto flex flex-col items-end gap-1">
            <div className="flex gap-2">
              {noDesktop() && (
                <button onClick={() => setEmailAberto(true)} disabled={!podeExportar} className={btnSecondary}>Enviar por e-mail</button>
              )}
              <button onClick={exportar} disabled={!podeExportar || exportando} className={btnPrimary}>
                {exportando ? 'Gerando PDF...' : 'Exportar PDF'}
              </button>
            </div>
            {!carregando && !temMedicao && <p className="text-[10px] text-texto-3">Sem medição nesse dia</p>}
            {temMedicao && faltando.length > 0 && (
              <p className="text-[10px] text-amber-600">Preencha: {faltando.join(' e ')}</p>
            )}
            {pdfSalvoEm && faltando.length === 0 && (
              <p className="text-[10px] text-green-700 dark:text-green-400 max-w-72 truncate" title={pdfSalvoEm}>PDF salvo em {pdfSalvoEm}</p>
            )}
            {avisoEmail && faltando.length === 0 && (
              <p className={`text-[10px] max-w-80 ${avisoEmail.ok ? 'text-green-700 dark:text-green-400' : 'text-red-500'}`}>{avisoEmail.texto}</p>
            )}
          </div>
        </div>
      </div>
      </div>

      <div className="px-4 pb-4">
      {/* A folha, como vai sair no PDF */}
      {carregando ? (
        <div className="flex items-center justify-center h-48 text-sm text-texto-3">Carregando...</div>
      ) : erro ? (
        <div className="flex items-center justify-center h-48 text-sm text-red-400">Erro: {erro}</div>
      ) : (
        <div className="relatorio-documento w-[210mm] max-w-full mx-auto flex flex-col gap-4" data-relatorio-pronto={pronto ? '1' : '0'}>
          {/* Página 1: cabeçalho e as seções gerais */}
          <Pagina comTimbre={comTimbre} timbreUrl={timbreUrl} margens={modelo?.margens}>
            {comTimbre ? (
              // Com timbre a marca já vem da folha: só a linha do que é este relatório
              <header className="relative flex items-baseline justify-between border-b border-zinc-300 pb-2 mb-5">
                <p className="text-[16pt] font-semibold">Relatório de medição</p>
                <p className="text-[10pt] text-zinc-500">
                  {dados.linhaNome} · {new Date(data + 'T00:00:00').toLocaleDateString('pt-BR')} · emitido em {emitidoEm.toLocaleDateString('pt-BR')} às {emitidoEm.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}
                </p>
              </header>
            ) : (
              <header className="flex items-start justify-between border-b-2 border-zinc-900 pb-3 mb-5">
                <div className="flex items-center gap-3">
                  <Logo tamanho={36} />
                  <div>
                    <p className="text-[16pt] font-semibold leading-tight">Relatório de medição</p>
                    <p className="text-[10pt] text-zinc-500">Smart Line Scan · Sanmartin</p>
                  </div>
                </div>
                <div className="text-right text-[10pt] text-zinc-500">
                  <p>{dados.linhaNome} · {new Date(data + 'T00:00:00').toLocaleDateString('pt-BR')}</p>
                  <p>Emitido em {emitidoEm.toLocaleDateString('pt-BR')} às {emitidoEm.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}</p>
                </div>
              </header>
            )}

            {temMedicao && sessao ? (
              <>
                <SecaoDadosMedicao dados={dados} agora={emitidoEm} />
                <SecaoResumoGeral sessao={sessao} agora={emitidoEm} />
                <SecaoIndicadores sessao={sessao} />
              </>
            ) : (
              <p className="text-[12pt] text-zinc-500 italic">Nenhuma máquina desta linha foi medida no dia escolhido.</p>
            )}
          </Pagina>

          {/* Uma página por máquina medida */}
          {sessoesMaquinas.map((m, i) => {
            const numero = 1 + i
            const partes = partesDaMaquina(blocos)
            const eventos = blocos.includes('linhaTempo') ? eventosCronologicos(m.sessao.eventos) : []
            // A linha do tempo começa embaixo do gráfico de paradas por hora (quando ele sai) e
            // continua em folhas próprias; sem esse gráfico, começa numa folha própria.
            const comecaAposGrafico = partes.includes('paradas')
            const fatias = fatiarEventos(eventos, comecaAposGrafico ? LINHAS_APOS_GRAFICO : LINHAS_POR_FOLHA)
            const tituloTempo = (f: number) => (
              <h3 className="text-[10pt] font-semibold uppercase tracking-wider text-texto-2 mb-2">
                Linha do tempo{fatias.length > 1 ? ` (${f + 1}/${fatias.length})` : ''}
              </h3>
            )
            return (
              <Fragment key={m.sessao.sessaoId}>
                {partes.map((parte, k) => (
                  <Pagina key={parte} comTimbre={comTimbre} timbreUrl={timbreUrl} margens={modelo?.margens}>
                    <PaginaMaquina numero={numero} sessao={m.sessao} blocos={blocos} parte={parte} comTitulo={k === 0} />
                    {parte === 'paradas' && fatias.length > 0 && (
                      <div className="mt-4">
                        {tituloTempo(0)}
                        <LinhaDoTempo eventos={fatias[0]} continuacao={false} />
                      </div>
                    )}
                  </Pagina>
                ))}
                {fatias.map((fatia, f) => {
                  if (comecaAposGrafico && f === 0) return null
                  // Sem nenhuma outra folha, a primeira da linha do tempo leva o título da máquina
                  const primeiraDaMaquina = partes.length === 0 && f === 0
                  return (
                    <Pagina key={f} comTimbre={comTimbre} timbreUrl={timbreUrl} margens={modelo?.margens}>
                      {primeiraDaMaquina && (
                        <h2 className="flex items-baseline justify-between gap-4 text-[14pt] font-semibold uppercase tracking-wider text-texto-2 border-b-2 border-texto pb-1 mb-3">
                          <span>{numero}. {m.sessao.maquinaNome}</span>
                        </h2>
                      )}
                      {tituloTempo(f)}
                      <LinhaDoTempo eventos={fatia} continuacao={f > 0} />
                    </Pagina>
                  )
                })}
              </Fragment>
            )
          })}
        </div>
      )}
      </div>

      <EnviarRelatorioEmailModal
        open={emailAberto}
        assuntoInicial={assuntoEmail}
        mensagemInicial={mensagemEmail}
        emailsSugeridos={emails}
        onRemoverEmail={v => clienteId && setEmails(removerDaLista(clienteId, 'emailsCliente', v))}
        onConfirmar={enviarEmail}
        onFechar={() => setEmailAberto(false)}
      />
    </div>
  )
}
