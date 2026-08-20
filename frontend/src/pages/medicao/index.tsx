import { useState, useEffect } from 'react'
import type { Linha, MaquinaLinha } from '../../types'
import { sessaoService, type SessaoDto } from '../../services/sessaoService'
import { useAuth } from '../../contexts/AuthContext'
import SelecaoMaquina from './SelecaoMaquina'
import TelaMedicao from './TelaMedicao'

interface SessaoAtiva {
  maquina: MaquinaLinha
  linha: Linha
  sessao: SessaoDto
  leiturasIniciais: Record<string, number>
}

const STORAGE_KEY = 'sessaoAtiva'
const ESTADO_MEDICAO_KEY = 'estadoMedicao'

export default function Medicao() {
  const { clienteId } = useAuth()
  const [sessaoAtiva, setSessaoAtiva] = useState<SessaoAtiva | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [restaurando, setRestaurando] = useState(true)

  // Ao entrar na tela, pergunta ao SERVIDOR (não só ao navegador) se o usuário tem uma sessão
  // ativa pendente — sobrevive a reiniciar o servidor, trocar de dispositivo ou limpar o navegador.
  // A sessão pode pertencer a um cliente DIFERENTE do que está selecionado no momento (Admin/
  // Desenvolvedor trocam de cliente livremente) — por isso montamos linha/máquina direto da
  // resposta do backend, sem depender de buscar as linhas do cliente atualmente selecionado.
  useEffect(() => {
    async function restaurar() {
      if (!clienteId) { setRestaurando(false); return }
      try {
        const ativa = await sessaoService.getSessaoAtivaDoUsuario()
        if (!ativa) { setRestaurando(false); return }

        const linha: Linha = {
          id: ativa.linhaId,
          clienteId: '',
          nome: ativa.linhaNome,
          ativo: true,
          maquinas: [],
        }

        const maquina: MaquinaLinha = {
          id: ativa.maquinaLinhaId,
          linhaId: ativa.linhaId,
          maquinaId: ativa.maquinaId,
          maquinaNome: ativa.maquinaNome,
          tipoColeta: 'Manual',
          velocidadeNominal: ativa.velocidadeNominal,
          critica: ativa.critica,
          medeProducao: ativa.medeProducao,
          ordem: 0,
          ativo: true,
          status: 'Rodando',
          oee: null,
          sessaoAtiva: true,
          sessaoAtivaId: ativa.sessaoId,
          ultimaSessaoFim: null,
        }

        const leituras = ativa.leituras.map(l => ({
          hora: new Date(l.hora).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }),
          valor: l.producao !== null ? String(l.producao) : '',
          inicial: l.inicial,
          extras: Object.fromEntries(Object.entries(l.extras).map(([k, v]) => [k, String(v)])),
        }))

        const estadoDetalhado = {
          sessaoId: ativa.sessaoId,
          status: ativa.status,
          leituras,
          segundosTotalParado: Math.floor(ativa.segundosTotalParadoMs / 1000),
          paradaAtiva: ativa.paradaAtivaId ? {
            id: ativa.paradaAtivaId,
            sessaoId: ativa.sessaoId,
            motivoId: null,
            inicio: ativa.paradaAtivaInicio,
            fim: null,
            fotoPath: null,
          } : null,
          leiturasSalvas: leituras.map(l => l.hora),
        }
        localStorage.setItem(ESTADO_MEDICAO_KEY, JSON.stringify(estadoDetalhado))

        const sessaoDto: SessaoDto = {
          id: ativa.sessaoId,
          maquinaLinhaId: ativa.maquinaLinhaId,
          usuarioId: '',
          inicio: ativa.inicio,
          fim: null,
          previsaoTermino: ativa.previsaoTermino,
          status: 'EmAndamento',
          tipoColeta: 'Manual',
          velocidadeNominal: ativa.velocidadeNominal,
          sobreVelocidade: ativa.sobreVelocidade,
          camposSelecionados: ativa.camposSelecionados,
        }

        const novaSessaoAtiva: SessaoAtiva = { maquina, linha, sessao: sessaoDto, leiturasIniciais: {} }
        localStorage.setItem(STORAGE_KEY, JSON.stringify(novaSessaoAtiva))
        setSessaoAtiva(novaSessaoAtiva)
      } catch (e) {
        console.error('[restaurar] ERRO:', e)
      } finally {
        setRestaurando(false)
      }
    }
    restaurar()
  }, [clienteId])

  async function handleIniciar(
    maquina: MaquinaLinha,
    linha: Linha,
    leiturasIniciais: Record<string, number>,
    params: {
      velocidadeNominal: number
      sobreVelocidade: number
      previsaoTermino: string | null
      tipoColeta: string
      campoMaquinaIds: string[]
    }
  ) {
    setErro(null)
    setLoading(true)
    try {
      const sessao = await sessaoService.abrir({
        maquinaLinhaId: maquina.id,
        velocidadeNominal: params.velocidadeNominal,
        sobreVelocidade: params.sobreVelocidade,
        previsaoTermino: params.previsaoTermino,
        tipoColeta: params.tipoColeta,
        campoMaquinaIds: params.campoMaquinaIds,
      })
      const nova: SessaoAtiva = { maquina, linha, sessao, leiturasIniciais }
      localStorage.setItem(STORAGE_KEY, JSON.stringify(nova))
      setSessaoAtiva(nova)
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao iniciar medição')
    } finally {
      setLoading(false)
    }
  }

  function handleFinalizar() {
    localStorage.removeItem(STORAGE_KEY)
    localStorage.removeItem(ESTADO_MEDICAO_KEY)
    setSessaoAtiva(null)
  }

  if (restaurando) {
    return (
      <div className="flex items-center justify-center h-48 text-sm text-zinc-400">
        Carregando...
      </div>
    )
  }

  if (sessaoAtiva) {
    return (
      <TelaMedicao
        maquina={sessaoAtiva.maquina}
        linha={sessaoAtiva.linha}
        sessao={sessaoAtiva.sessao}
        leiturasIniciais={sessaoAtiva.leiturasIniciais}
        onFinalizar={handleFinalizar}
      />
    )
  }

  return (
    <>
      {erro && (
        <div className="max-w-lg mx-auto mt-4 px-4">
          <div className="bg-red-50 dark:bg-red-950 border border-red-200 dark:border-red-800 px-3 py-2 text-xs text-red-600 dark:text-red-400">
            {erro}
          </div>
        </div>
      )}
      <SelecaoMaquina onIniciar={handleIniciar} loading={loading} />
    </>
  )
}