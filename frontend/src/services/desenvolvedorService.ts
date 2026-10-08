// Área do Desenvolvedor: log do backend e simulador de WISE (api/dev, só nível Desenvolvedor).
import { api } from './api'

export type NivelLog = 'Information' | 'Warning' | 'Error' | 'Critical'

export interface LinhaLogDto {
  sequencia: number
  instante: string
  nivel: NivelLog
  categoria: string
  mensagem: string
  excecao: string | null
}

export interface LogsDto {
  ultimaSequencia: number
  capacidade: number
  linhas: LinhaLogDto[]
}

// O que o Desenvolvedor escolhe ao ligar o simulador (tempos em minutos).
export interface OpcoesSimuladorDto {
  maquinas: number
  intervaloSegundos: number
  garrafasPorMinuto: number[] | null
  garrafasPorPulso: number[] | null
  rejeitoPercentual: number
  primeiroIp: number
  automatico: boolean
  rodandoMedioMin: number
  paradaMinimaMin: number
  paradaMaximaMin: number
  quedas: boolean
  quedaMediaMin: number
  quedaMinimaMin: number
  quedaMaximaMin: number
  portaHttp: number
}

export type CenarioSimulado =
  | 'Rodando' | 'FaltaGarrafas' | 'AbaixoAcumuloMinimo' | 'SaidaGarrafasBloqueada'
  | 'SaidaCaixasBloqueada' | 'ParadaSemCausa' | 'Desligado'

export interface MaquinaSimuladaDto {
  numero: number
  enderecoIp: string
  cenario: CenarioSimulado
  falha: 'SemRede' | 'SemEnergia' | null
  contadorProducao: number
  contadorRejeito: number
  conectada: boolean
  automatico: boolean
  proximaMudanca: string | null
  proximaMudancaComunicacao: string | null
  garrafasPorMinuto: number
  garrafasPorPulso: number
  interfaceHttp: boolean
}

export interface SimuladorDto {
  ligado: boolean
  ligadoEm: string | null
  portaBroker: number
  opcoes: OpcoesSimuladorDto | null
  maquinas: MaquinaSimuladaDto[]
}

// As mesmas ações do console do simulador.
export type AcaoSimulada =
  | 'rodando' | 'falta' | 'minimo' | 'saida' | 'caixas' | 'parada'
  | 'desligar' | 'semrede' | 'reiniciar' | 'auto' | 'manual'

export const desenvolvedorService = {
  logs: (depoisDe = 0, nivel?: NivelLog | '', texto?: string, maximo = 500) => {
    const p = new URLSearchParams({ depoisDe: String(depoisDe), maximo: String(maximo) })
    if (nivel) p.set('nivel', nivel)
    if (texto?.trim()) p.set('texto', texto.trim())
    return api.get<LogsDto>(`/dev/logs?${p}`)
  },

  simulador: () => api.get<SimuladorDto>('/dev/simulador'),
  ligarSimulador: (opcoes: OpcoesSimuladorDto) => api.post<SimuladorDto>('/dev/simulador/ligar', opcoes),
  desligarSimulador: () => api.post<SimuladorDto>('/dev/simulador/desligar', {}),
  /** numero 0 = todas as máquinas. */
  comando: (numero: number, acao: AcaoSimulada) =>
    api.post<SimuladorDto>(`/dev/simulador/maquinas/${numero}/comando`, { acao }),
}
