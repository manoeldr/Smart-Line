// Aba "Licença" da Área do Desenvolvedor: gera a chave de licença de um MAC (o mesmo cálculo do
// SmartLine.LicenseGenerator), para ativar o sistema no computador do cliente. Só aparece quando
// o backend está em desenvolvimento (fora dele a API responde 404 e a aba some).
import { useEffect, useState } from 'react'
import { desenvolvedorService, type ChaveGeradaDto, type SituacaoLicencaDevDto } from '../../services/desenvolvedorService'
import { mensagemErro } from '../../services/api'
import { btnPrimarySm, btnSecondarySm } from '../../styles/buttons'
import { inputBase, label as labelClass } from '../../styles/inputs'
import { cardPadded } from '../../styles/cards'

function formatarMac(mac: string) {
  return mac.match(/.{1,2}/g)?.join('-') ?? mac
}

export default function AbaLicenca() {
  const [situacao, setSituacao] = useState<SituacaoLicencaDevDto | null>(null)
  const [mac, setMac] = useState('')
  const [chave, setChave] = useState<ChaveGeradaDto | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [gerando, setGerando] = useState(false)
  const [copiado, setCopiado] = useState(false)

  useEffect(() => {
    let ativo = true
    desenvolvedorService.licenca()
      .then(s => { if (ativo) setSituacao(s) })
      .catch(e => { if (ativo) setErro(mensagemErro(e, 'Não foi possível consultar a licença.')) })
    return () => { ativo = false }
  }, [])

  const macLimpo = mac.trim().toUpperCase().replace(/[^0-9A-F]/g, '')
  const macValido = macLimpo.length === 12

  async function gerar() {
    setGerando(true)
    setErro(null)
    setCopiado(false)
    try {
      setChave(await desenvolvedorService.gerarChave(macLimpo))
    } catch (e) {
      setErro(mensagemErro(e, 'Não foi possível gerar a chave.'))
    } finally {
      setGerando(false)
    }
  }

  async function copiar() {
    if (!chave) return
    try {
      await navigator.clipboard.writeText(chave.chave)
      setCopiado(true)
    } catch { /* sem área de transferência: o texto fica na tela para selecionar */ }
  }

  return (
    <div className="flex flex-col gap-4 max-w-2xl">
      <div className={`${cardPadded} flex flex-col gap-3`}>
        <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">Gerar chave de licença</p>
        <p className="text-xs text-zinc-500">
          Digite o MAC que aparece na tela de ativação do computador do cliente. A chave vale para aquele MAC;
          o sistema aceita se ele for de qualquer placa de rede física daquele computador.
        </p>
        <div className="flex items-end gap-2">
          <div className="flex-1">
            <label className={labelClass}>MAC address</label>
            <input
              className={`${inputBase} font-mono`}
              value={mac}
              onChange={e => { setMac(e.target.value); setChave(null); setCopiado(false) }}
              onKeyDown={e => { if (e.key === 'Enter' && macValido && !gerando) gerar() }}
              placeholder="00-11-22-33-44-AA"
              autoFocus
            />
          </div>
          <button onClick={gerar} disabled={!macValido || gerando} className={btnPrimarySm}>
            {gerando ? 'Gerando...' : 'Gerar chave'}
          </button>
        </div>
        {mac && !macValido && <p className="text-[10px] text-zinc-400">O MAC tem 12 caracteres hexadecimais (com ou sem separadores).</p>}
        {erro && <p className="text-xs text-red-500">{erro}</p>}

        {chave && (
          <div className="bg-zinc-50 dark:bg-zinc-800 p-3 flex flex-col gap-1">
            <p className="text-[10px] text-zinc-400">MAC {formatarMac(chave.macAddress)}</p>
            <div className="flex items-center gap-3">
              <p className="font-mono text-base text-zinc-900 dark:text-zinc-100 select-all">{chave.chave}</p>
              <button onClick={copiar} className={btnSecondarySm}>{copiado ? 'Copiado' : 'Copiar'}</button>
            </div>
          </div>
        )}
      </div>

      {situacao && (
        <div className={`${cardPadded} flex flex-col gap-2`}>
          <p className="text-xs font-medium text-zinc-900 dark:text-zinc-100">Este computador</p>
          <p className="text-xs text-zinc-500">
            Licença: <span className={situacao.licencaAtiva ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400'}>
              {situacao.licencaAtiva ? 'ativa' : 'não ativada'}
            </span>
          </p>
          <div className="text-xs text-zinc-500">
            Placas de rede físicas:
            <ul className="mt-1 flex flex-col gap-1">
              {situacao.macsFisicos.map(m => (
                <li key={m} className="flex items-center gap-2">
                  <span className="font-mono text-zinc-900 dark:text-zinc-100">{formatarMac(m)}</span>
                  {m === situacao.macPreferido && <span className="text-[10px] text-zinc-400">(a tela de ativação mostra este)</span>}
                  <button onClick={() => { setMac(m); setChave(null) }} className="text-[10px] text-blue-600 dark:text-blue-400 hover:underline">usar</button>
                </li>
              ))}
            </ul>
          </div>
        </div>
      )}
    </div>
  )
}
