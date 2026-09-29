// WISE vistos pelo SmartLine e diagnóstico da comunicação: lista, Validar entradas, ping e broker.
// Não há cadastro de WISE: o IP é informado ao iniciar a medição e ele fica livre ao finalizar.
import { api } from './api'

export type TipoCanal = 'Contador' | 'Estado'

// A coleta em andamento que está usando um WISE.
export interface MedicaoDoWiseDto {
  acompanhamentoId: string
  maquinaLinhaId: string
  maquina: string
  linha: string
  cliente: string
  usuario: string
  iniciadoEmUtc: string
}

// Um WISE: conectado ao broker agora, que publicou desde que o backend subiu, ou em medição.
export interface WiseDto {
  enderecoIp: string
  conectado: boolean
  clientId: string | null
  ultimaMensagemUtc: string | null
  mensagens: number
  // Nulo = livre (nenhuma medição usando)
  medicao: MedicaoDoWiseDto | null
}

export interface StatusColetaIotDto {
  brokerHabilitado: boolean
  brokerEmExecucao: boolean
  porta: number
  wiseConectados: number
  mensagensProcessadas: number
  mensagensDescartadas: number
  mensagensPerdidasFilaCheia: number
}

// Uma entrada do WISE agora, já em texto (Validar entradas).
export interface EntradaAoVivoDto {
  canal: string
  entrada: number
  tipo: TipoCanal
  nome: string
  recebida: boolean
  lidoEmUtc: string | null
  valor: number | null
  incremento: number | null
  intervaloSegundos: number | null
  valorBruto: boolean | null
  emAlarme: boolean | null
  texto: string | null
}

export interface EntradasDoWiseDto {
  enderecoIp: string
  conectado: boolean
  ultimaMensagemUtc: string | null
  medicao: MedicaoDoWiseDto | null
  entradas: EntradaAoVivoDto[]
}

export interface RespostaPingDto {
  sequencia: number
  respondeu: boolean
  tempoMs: number | null
  situacao: string
}

export interface ResultadoPingDto {
  enderecoIp: string
  respostas: RespostaPingDto[]
  enviados: number
  recebidos: number
  perdaPercentual: number
  tempoMinimoMs: number | null
  tempoMaximoMs: number | null
  tempoMedioMs: number | null
}

export const dispositivoIotService = {
  // Administrador, Desenvolvedor e Auditor (o Configurar medição usa para a situação do IP)
  listar: () => api.get<WiseDto[]>('/dispositivos-iot'),

  // Só Administrador e Desenvolvedor
  status: () => api.get<StatusColetaIotDto>('/dispositivos-iot/status'),
  entradasDoIp: (ip: string) => api.get<EntradasDoWiseDto>(`/dispositivos-iot/entradas?ip=${encodeURIComponent(ip)}`),
  ping: (enderecoIp: string) => api.post<ResultadoPingDto>('/dispositivos-iot/ping', { enderecoIp }),
}
