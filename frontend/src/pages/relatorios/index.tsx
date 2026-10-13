// Tela de Relatórios — escolhe o recorte (por enquanto: uma linha, um dia), preenche o que é
// digitado, confere a folha A4 na tela e exporta em PDF. A exportação é a impressão da própria
// folha (Ctrl+P / "Salvar como PDF"); o resto da tela some na impressão (ver index.css).
// Produto e Responsável do Cliente são obrigatórios para exportar.
import { useEffect, useMemo, useState } from 'react'
import { useAuth } from '../../contexts/AuthContext'
import { linhaService } from '../../services/linhaService'
import { clienteService } from '../../services/clienteService'
import { sessaoDetalheService, type SessaoDetalheDto } from '../../services/sessaoDetalheService'
import type { Linha } from '../../types'
import CampoData from '../../components/CampoData'
import CampoComLista from '../../components/CampoComLista'
import { inputMd, label } from '../../styles/inputs'
import { btnPrimary } from '../../styles/buttons'
import { cardPadded } from '../../styles/cards'
import { maquinaDeReferencia } from './referencia'
import { lerLista, lembrarNaLista, removerDaLista } from './listasRelatorio'
import SecaoDadosMedicao, { type DadosMedicao } from './SecaoDadosMedicao'
import SecaoResumoGeral from './SecaoResumoGeral'
import Logo from '../../components/Logo'
import { useModeloRelatorio } from '../../hooks/useModeloRelatorio'

function hojeTexto() {
  return new Date().toISOString().slice(0, 10)
}

export default function Relatorios() {
  const { usuario, clienteId } = useAuth()
  // Folha timbrada (Configurações → Relatório): fundo de cada página e margens do conteúdo
  const { modelo, imagemUrl: timbreUrl } = useModeloRelatorio()
  const comTimbre = !!modelo?.existe && !!timbreUrl
  const [clienteNome, setClienteNome] = useState('')
  const [linhas, setLinhas] = useState<Linha[]>([])
  const [linhaId, setLinhaId] = useState('')
  const [data, setData] = useState(hojeTexto)
  const [datasComSessao, setDatasComSessao] = useState<string[]>([])

  // A linha com as sessões do dia escolhido, e o detalhe da sessão da máquina de referência
  const [linhaDoDia, setLinhaDoDia] = useState<Linha | null>(null)
  const [sessao, setSessao] = useState<SessaoDetalheDto | null>(null)
  const [carregando, setCarregando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  // Instante da emissão (capa) e referência do "até agora" de uma medição em andamento
  const [emitidoEm, setEmitidoEm] = useState(() => new Date())

  // Campos digitados
  const [produto, setProduto] = useState('')
  const [responsavelCliente, setResponsavelCliente] = useState('')
  const [produtos, setProdutos] = useState<string[]>([])
  const [responsaveis, setResponsaveis] = useState<string[]>([])

  // Nome do cliente e as linhas dele
  useEffect(() => {
    if (!clienteId) return
    const id = clienteId
    async function carregar() {
      // Campos digitados e listas são do cliente: trocou de cliente, recomeça
      setProdutos(lerLista(id, 'produtos'))
      setResponsaveis(lerLista(id, 'responsaveisCliente'))
      setProduto('')
      setResponsavelCliente('')
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

  // Dias com sessão da linha (bolinha no calendário)
  useEffect(() => {
    if (!clienteId || !linhaId) return
    let ativo = true
    clienteService.getDatasComSessao(clienteId, linhaId)
      .then(ds => { if (ativo) setDatasComSessao(ds) })
      .catch(() => { if (ativo) setDatasComSessao([]) })
    return () => { ativo = false }
  }, [clienteId, linhaId])

  // Sessões do dia na linha → máquina de referência → detalhe da sessão dela
  useEffect(() => {
    if (!clienteId || !linhaId) return
    const id = clienteId
    let ativo = true
    async function carregar() {
      setCarregando(true)
      setErro(null)
      try {
        const ls = await linhaService.getLinhasByCliente(id, data)
        const linha = ls.find(l => l.id === linhaId) ?? null
        const ref = maquinaDeReferencia(linha)
        const detalhe = ref?.sessaoDia ? await sessaoDetalheService.getSessaoDetalhe(ref.sessaoDia.sessaoId) : null
        if (!ativo) return
        setLinhaDoDia(linha)
        setSessao(detalhe)
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
    window.print()
  }

  return (
    <div className="p-4 flex flex-col gap-4">

      {/* Recorte e campos digitados — somem na impressão */}
      <div className={`${cardPadded} relatorio-controles flex flex-col gap-3`}>
        <div className="flex items-end gap-3 flex-wrap">
          <div className="flex flex-col gap-1.5">
            <label className={label}>Linha</label>
            <select value={linhaId} onChange={e => setLinhaId(e.target.value)} className={inputMd}>
              {linhas.map(l => <option key={l.id} value={l.id}>{l.nome}</option>)}
            </select>
          </div>
          <div className="flex flex-col gap-1.5">
            <label className={label}>Data</label>
            <CampoData valor={data} onChange={setData} datasComSessao={datasComSessao} max={hojeTexto()} />
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
          <div className="ml-auto flex flex-col items-end gap-1">
            <button onClick={exportar} disabled={!podeExportar} className={btnPrimary}>Exportar PDF</button>
            {!carregando && !temMedicao && <p className="text-[10px] text-texto-3">Sem medição nesse dia</p>}
            {temMedicao && faltando.length > 0 && (
              <p className="text-[10px] text-amber-600">Preencha: {faltando.join(' e ')}</p>
            )}
          </div>
        </div>
      </div>

      {/* A folha, como vai sair no PDF */}
      {carregando ? (
        <div className="flex items-center justify-center h-48 text-sm text-texto-3">Carregando...</div>
      ) : erro ? (
        <div className="flex items-center justify-center h-48 text-sm text-red-400">Erro: {erro}</div>
      ) : (
        <div
          className="relatorio-folha mx-auto bg-white text-zinc-900 shadow-md"
          style={comTimbre && modelo ? {
            padding: `${modelo.margens.superior}mm ${modelo.margens.direita}mm ${modelo.margens.inferior}mm ${modelo.margens.esquerda}mm`,
          } : undefined}
        >
          {comTimbre && <img src={timbreUrl!} alt="" className="relatorio-timbre" />}

          {comTimbre ? (
            // Com timbre a marca já vem da folha: só a linha do que é este relatório
            <header className="relative flex items-baseline justify-between border-b border-zinc-300 pb-2 mb-5">
              <p className="text-base font-semibold">Relatório de medição</p>
              <p className="text-xs text-zinc-500">
                {dados.linhaNome} · {new Date(data + 'T00:00:00').toLocaleDateString('pt-BR')} · emitido em {emitidoEm.toLocaleDateString('pt-BR')} às {emitidoEm.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}
              </p>
            </header>
          ) : (
            <header className="flex items-start justify-between border-b-2 border-zinc-900 pb-3 mb-5">
              <div className="flex items-center gap-3">
                <Logo tamanho={36} />
                <div>
                  <p className="text-base font-semibold leading-tight">Relatório de medição</p>
                  <p className="text-xs text-zinc-500">Smart Line Scan · Sanmartin</p>
                </div>
              </div>
              <div className="text-right text-xs text-zinc-500">
                <p>{dados.linhaNome} · {new Date(data + 'T00:00:00').toLocaleDateString('pt-BR')}</p>
                <p>Emitido em {emitidoEm.toLocaleDateString('pt-BR')} às {emitidoEm.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })}</p>
              </div>
            </header>
          )}

          {temMedicao && sessao ? (
            <>
              <SecaoDadosMedicao dados={dados} agora={emitidoEm} />
              <SecaoResumoGeral sessao={sessao} agora={emitidoEm} />
            </>
          ) : (
            <p className="text-sm text-zinc-500 italic">Nenhuma máquina desta linha foi medida no dia escolhido.</p>
          )}
        </div>
      )}
    </div>
  )
}
