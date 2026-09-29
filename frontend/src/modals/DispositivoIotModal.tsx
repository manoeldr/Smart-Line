// Cadastro/edição de um WISE: nome, IP fixo configurado nele e a máquina em que está.
// A máquina é escolhida por cliente → linha → máquina. Máquinas que já têm WISE aparecem
// desabilitadas (um WISE por máquina). Recusas do backend (IP repetido, IP inválido,
// coleta ligada) aparecem no topo do modal.
import { useEffect, useState } from 'react'
import { configuracaoService, type ClienteConfDto, type LinhaConfDto } from '../services/configuracaoService'
import { linhaMaquinaService, type MaquinaLinhaConfDto } from '../services/linhaMaquinaService'
import { dispositivoIotService, type DispositivoIotDto } from '../services/dispositivoIotService'
import { mensagemErro } from '../services/api'
import { btnPrimary, btnSecondarySm } from '../styles/buttons'
import { inputBase, label, checkbox } from '../styles/inputs'
import { modalOverlay, modalPanel, modalHeader, modalTitle, modalSubtitle, modalBody, modalFooter } from '../styles/modals'

interface Props {
  // Nulo = novo WISE
  dispositivo: DispositivoIotDto | null
  // IP já preenchido (cadastro a partir de um WISE aguardando cadastro)
  ipSugerido?: string
  // Todos os WISE, para marcar as máquinas que já têm um
  dispositivos: DispositivoIotDto[]
  onFechar: () => void
  onSalvo: () => void
}

export default function DispositivoIotModal({ dispositivo, ipSugerido, dispositivos, onFechar, onSalvo }: Props) {
  const [clientes, setClientes] = useState<ClienteConfDto[]>([])
  const [linhas, setLinhas] = useState<LinhaConfDto[]>([])
  const [maquinas, setMaquinas] = useState<MaquinaLinhaConfDto[]>([])

  const [nome, setNome] = useState(dispositivo?.nome ?? '')
  const [enderecoIp, setEnderecoIp] = useState(dispositivo?.enderecoIp ?? ipSugerido ?? '')
  const [clienteId, setClienteId] = useState('')
  const [linhaId, setLinhaId] = useState(dispositivo?.linhaId ?? '')
  const [maquinaLinhaId, setMaquinaLinhaId] = useState(dispositivo?.maquinaLinhaId ?? '')
  const [ativo, setAtivo] = useState(dispositivo?.ativo ?? true)

  const [salvando, setSalvando] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  // Clientes e linhas uma vez; na edição, descobre o cliente pela linha do WISE.
  useEffect(() => {
    let ativo = true
    Promise.all([configuracaoService.getClientes(), configuracaoService.getLinhas()])
      .then(([c, l]) => {
        if (!ativo) return
        setClientes(c.filter(x => x.ativo))
        setLinhas(l.filter(x => x.ativo))
        if (dispositivo) setClienteId(l.find(x => x.id === dispositivo.linhaId)?.clienteId ?? '')
      })
      .catch(e => { if (ativo) setErro(mensagemErro(e, 'Erro ao carregar clientes e linhas.')) })
    return () => { ativo = false }
  }, [dispositivo])

  // Máquinas da linha escolhida.
  useEffect(() => {
    if (!linhaId) return
    let ativo = true
    linhaMaquinaService.getMaquinas(linhaId)
      .then(m => { if (ativo) setMaquinas(m) })
      .catch(e => { if (ativo) setErro(mensagemErro(e, 'Erro ao carregar as máquinas da linha.')) })
    return () => { ativo = false }
  }, [linhaId])

  function escolherCliente(id: string) {
    setClienteId(id)
    setLinhaId('')
    setMaquinaLinhaId('')
    setMaquinas([])
  }

  function escolherLinha(id: string) {
    setLinhaId(id)
    setMaquinaLinhaId('')
    setMaquinas([])
  }

  function escolherMaquina(id: string) {
    setMaquinaLinhaId(id)
    const m = maquinas.find(x => x.id === id)
    if (m && !nome.trim()) setNome(`WISE ${m.maquinaNome}`)
  }

  // Máquina que já tem outro WISE (a do próprio WISE em edição fica livre).
  function wiseDaMaquina(id: string) {
    return dispositivos.find(d => d.maquinaLinhaId === id && d.id !== dispositivo?.id)
  }

  async function salvar() {
    setSalvando(true)
    setErro(null)
    try {
      const dados = { maquinaLinhaId, nome: nome.trim(), enderecoIp: enderecoIp.trim(), ativo }
      if (dispositivo) await dispositivoIotService.editar(dispositivo.id, dados)
      else await dispositivoIotService.criar(dados)
      onSalvo()
    } catch (e) {
      setErro(mensagemErro(e, 'Erro ao salvar o WISE.'))
    } finally {
      setSalvando(false)
    }
  }

  const podeSalvar = !!nome.trim() && !!enderecoIp.trim() && !!maquinaLinhaId && !salvando

  return (
    <div className={modalOverlay}>
      <div className={`${modalPanel} w-[440px] max-h-[90vh]`}>
        <div className={modalHeader}>
          <p className={modalTitle}>{dispositivo ? 'Editar WISE' : 'Novo WISE'}</p>
          <p className={modalSubtitle}>O IP é o IP fixo configurado no WISE. É por ele que o sistema sabe de qual máquina é cada mensagem.</p>
        </div>

        <div className={modalBody}>
          {erro && (
            <div className="bg-red-50 dark:bg-red-950 border border-red-200 dark:border-red-800 px-3 py-2 text-xs text-red-600 dark:text-red-400">
              {erro}
            </div>
          )}

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className={label}>IP do WISE</label>
              <input value={enderecoIp} onChange={e => setEnderecoIp(e.target.value)} placeholder="192.168.10.21" className={inputBase} />
            </div>
            <div>
              <label className={label}>Nome</label>
              <input value={nome} onChange={e => setNome(e.target.value)} placeholder="WISE Enchedora" className={inputBase} />
            </div>
          </div>

          <div>
            <label className={label}>Cliente</label>
            <select value={clienteId} onChange={e => escolherCliente(e.target.value)} className={inputBase}>
              <option value="">Selecionar cliente...</option>
              {clientes.map(c => <option key={c.id} value={c.id}>{c.nome}</option>)}
            </select>
          </div>

          <div>
            <label className={label}>Linha</label>
            <select value={linhaId} onChange={e => escolherLinha(e.target.value)} disabled={!clienteId} className={inputBase}>
              <option value="">Selecionar linha...</option>
              {linhas.filter(l => l.clienteId === clienteId).map(l => <option key={l.id} value={l.id}>{l.nome}</option>)}
            </select>
          </div>

          <div>
            <label className={label}>Máquina</label>
            <select value={maquinaLinhaId} onChange={e => escolherMaquina(e.target.value)} disabled={!linhaId} className={inputBase}>
              <option value="">Selecionar máquina...</option>
              {[...maquinas].sort((a, b) => a.ordem - b.ordem).map(m => {
                const outro = wiseDaMaquina(m.id)
                return (
                  <option key={m.id} value={m.id} disabled={!!outro}>
                    {m.maquinaNome}{outro ? ` (já tem o WISE ${outro.enderecoIp})` : ''}
                  </option>
                )
              })}
            </select>
          </div>

          {dispositivo && (
            <label className="flex items-center gap-2 text-xs text-zinc-700 dark:text-zinc-300">
              <input type="checkbox" checked={ativo} onChange={e => setAtivo(e.target.checked)} className={checkbox} />
              Ativo (inativo: as mensagens deste IP são ignoradas)
            </label>
          )}
        </div>

        <div className={modalFooter}>
          <button onClick={onFechar} disabled={salvando} className={btnSecondarySm}>Cancelar</button>
          <button onClick={salvar} disabled={!podeSalvar} className={btnPrimary}>
            {salvando ? 'Salvando...' : 'Salvar'}
          </button>
        </div>
      </div>
    </div>
  )
}
