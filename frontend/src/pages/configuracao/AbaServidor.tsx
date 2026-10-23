// Aba "Servidor" da tela de Configurações — Administrador/Desenvolvedor.
//
// Define o papel DESTE computador: servidor (guarda o banco e atende as estações) ou estação
// (só a tela, puxando tudo do servidor). Quem grava é o aplicativo do computador, não a API:
// numa estação a tela vem do servidor, então um pedido pela API configuraria o servidor em vez
// da estação. Por isso tudo aqui passa pela ponte do aplicativo (ver utils/desktop.ts) e esta
// aba só funciona dentro do aplicativo instalado.
import { useEffect, useState } from 'react'
import { btnPrimary, btnSecondary } from '../../styles/buttons'
import { cardPadded } from '../../styles/cards'
import { inputMd, label } from '../../styles/inputs'
import { noDesktop, situacaoDesktop, pedirAoDesktop, ouvirDesktop } from '../../utils/desktop'

type Papel = 'servidor' | 'cliente'
type Aviso = { ok: boolean; texto: string }

export default function AbaServidor() {
  const situacao = situacaoDesktop()
  const [papel, setPapel] = useState<Papel>(situacao?.modoCliente ? 'cliente' : 'servidor')
  const [endereco, setEndereco] = useState(situacao?.endereco ?? '')
  const [testando, setTestando] = useState(false)
  const [avisoTeste, setAvisoTeste] = useState<Aviso | null>(null)
  const [avisoSalvar, setAvisoSalvar] = useState<Aviso | null>(null)
  const [liberando, setLiberando] = useState(false)
  const [avisoFirewall, setAvisoFirewall] = useState<Aviso | null>(null)

  useEffect(() => ouvirDesktop(resposta => {
    const aviso = { ok: !!resposta.ok, texto: resposta.mensagem ?? '' }
    if (resposta.tipo === 'servidorTestado') { setTestando(false); setAvisoTeste(aviso) }
    if (resposta.tipo === 'servidorSalvo') setAvisoSalvar(aviso)
    if (resposta.tipo === 'firewallResultado') { setLiberando(false); setAvisoFirewall(aviso) }
  }), [])

  // Fora do aplicativo (navegador) não há o que configurar: quem decide é o computador.
  if (!noDesktop() || !situacao) {
    return (
      <div className={`${cardPadded} max-w-2xl`}>
        <p className="text-sm font-medium text-texto mb-1">Servidor</p>
        <p className="text-xs text-texto-3">
          Esta configuração é de cada computador e só pode ser feita no Smart Line Scan instalado
          nele. Abra o programa no computador que você quer configurar e volte nesta aba.
        </p>
      </div>
    )
  }

  const mudou = papel !== (situacao.modoCliente ? 'cliente' : 'servidor') || endereco.trim() !== situacao.endereco
  const podeSalvar = mudou && (papel === 'servidor' || endereco.trim().length > 0)

  function escolher(novo: Papel) {
    setPapel(novo)
    setAvisoTeste(null)
    setAvisoSalvar(null)
  }

  function testar() {
    setTestando(true)
    setAvisoTeste(null)
    pedirAoDesktop({ tipo: 'testarServidor', endereco: endereco.trim() })
  }

  function salvar() {
    setAvisoSalvar(null)
    pedirAoDesktop({ tipo: 'salvarServidor', modo: papel, endereco: endereco.trim() })
  }

  function liberarFirewall() {
    setLiberando(true)
    setAvisoFirewall(null)
    pedirAoDesktop({ tipo: 'liberarFirewall' })
  }

  return (
    <div className="flex flex-col gap-6 max-w-2xl">

      {/* ── Papel deste computador ──────────────────────────── */}
      <div className={cardPadded}>
        <p className="text-sm font-medium text-texto mb-1">Este computador</p>
        <p className="text-xs text-texto-3 mb-4">
          Em uma rede com vários computadores, um guarda os dados (servidor) e os outros só abrem
          as telas (estações). Escolha o que este computador vai fazer.
        </p>

        <div className="flex flex-col gap-2">
          <Opcao
            marcada={papel === 'servidor'}
            onClick={() => escolher('servidor')}
            titulo="Utilizar esse PC como servidor"
            descricao="Guarda o banco de dados, recebe os WISE e atende as estações da rede."
          />
          <Opcao
            marcada={papel === 'cliente'}
            onClick={() => escolher('cliente')}
            titulo="Conectar-se a um servidor"
            descricao="Só mostra as telas: os dados ficam no computador servidor."
          />
        </div>

        {papel === 'cliente' && (
          <div className="mt-4 flex items-end gap-2 flex-wrap">
            <div className="flex flex-col">
              <label className={label}>Endereço do servidor</label>
              <input
                value={endereco}
                onChange={e => { setEndereco(e.target.value); setAvisoTeste(null); setAvisoSalvar(null) }}
                placeholder="Ex.: 192.168.10.171"
                className={`${inputMd} w-56`}
              />
            </div>
            <button onClick={testar} disabled={testando || !endereco.trim()} className={btnSecondary}>
              {testando ? 'Testando...' : 'Testar conexão'}
            </button>
          </div>
        )}

        {papel === 'cliente' && (
          <p className="text-[11px] text-texto-3 mt-2">
            Informe o endereço que aparece no computador servidor (nesta mesma aba, no campo
            "Endereço deste servidor na rede"). A porta {situacao.porta} é usada automaticamente.
          </p>
        )}

        {avisoTeste && <Mensagem aviso={avisoTeste} />}

        {papel === 'servidor' && situacao.ips.length > 0 && (
          <div className="mt-4 bg-superficie-2 px-3 py-2">
            <p className="text-[11px] text-texto-3 mb-1">Endereço deste servidor na rede</p>
            <p className="text-sm text-texto">
              {situacao.ips.map(ip => `${ip}:${situacao.porta}`).join('  ·  ')}
            </p>
            <p className="text-[11px] text-texto-3 mt-1">
              É este endereço que as estações devem informar em "Conectar-se a um servidor".
              {situacao.ips.length > 1 && ' Aparece mais de um porque este computador está em mais de uma rede: use o da rede em que as estações estão.'}
            </p>
          </div>
        )}

        {avisoSalvar && <Mensagem aviso={avisoSalvar} />}

        <div className="mt-4 flex items-center gap-3">
          <button onClick={salvar} disabled={!podeSalvar} className={btnPrimary}>Salvar</button>
          {mudou && (
            <span className="text-[11px] text-texto-3">
              O Smart Line Scan vai reiniciar para a mudança valer.
            </span>
          )}
          {!mudou && (
            <span className="text-[11px] text-texto-3">
              {situacao.modoCliente
                ? `Hoje este computador é uma estação do servidor ${situacao.endereco}.`
                : 'Hoje este computador é o servidor.'}
            </span>
          )}
        </div>
      </div>

      {/* ── Firewall (só faz sentido no servidor) ───────────── */}
      {papel === 'servidor' && (
        <div className={cardPadded}>
          <p className="text-sm font-medium text-texto mb-1">Liberação na rede</p>
          <p className="text-xs text-texto-3 mb-4">
            Por segurança, o Windows bloqueia conexões de outros computadores. Para as estações
            abrirem as telas e os WISE enviarem as leituras, estas liberações precisam existir
            neste computador. O Windows vai pedir sua confirmação uma vez.
          </p>

          <ul className="text-[11px] text-texto-3 mb-4 flex flex-col gap-0.5">
            <li>· Porta {situacao.porta} — as estações abrem as telas do sistema</li>
            <li>· Porta 1883 — os WISE enviam as leituras das máquinas</li>
            <li>· Ping — ajuda a descobrir problemas de rede</li>
          </ul>

          {avisoFirewall && <Mensagem aviso={avisoFirewall} />}

          <button onClick={liberarFirewall} disabled={liberando} className={`${btnPrimary} mt-1`}>
            {liberando ? 'Liberando...' : 'Liberar agora'}
          </button>
        </div>
      )}
    </div>
  )
}

function Opcao({ marcada, onClick, titulo, descricao }: { marcada: boolean; onClick: () => void; titulo: string; descricao: string }) {
  return (
    <button
      onClick={onClick}
      className={`text-left px-3 py-2.5 border transition-colors ${
        marcada
          ? 'border-accent-600 bg-accent-50 dark:bg-accent-950'
          : 'border-borda hover:bg-superficie-2'
      }`}
    >
      <span className="flex items-center gap-2">
        <span className={`w-3 h-3 rounded-full border flex items-center justify-center ${marcada ? 'border-accent-600' : 'border-borda-forte'}`}>
          {marcada && <span className="w-1.5 h-1.5 rounded-full bg-accent-600" />}
        </span>
        <span className="text-xs font-medium text-texto">{titulo}</span>
      </span>
      <span className="block text-[11px] text-texto-3 mt-1 ml-5">{descricao}</span>
    </button>
  )
}

function Mensagem({ aviso }: { aviso: Aviso }) {
  const cor = aviso.ok
    ? 'text-green-700 dark:text-green-400 bg-green-50 dark:bg-green-950'
    : 'text-red-600 dark:text-red-400 bg-red-50 dark:bg-red-950'
  return <p className={`text-[11px] px-3 py-2 mt-3 ${cor}`}>{aviso.texto}</p>
}
