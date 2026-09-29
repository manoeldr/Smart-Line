// Cadastro dos WISE (Semi Automático), diagnóstico do broker e Validar entradas.
import { api } from './api'

export type SituacaoConexaoWise = 'Conectado' | 'Desconectado' | 'NaoCadastrado'
export type TipoCanal = 'Contador' | 'Estado'

export interface DispositivoIotDto {
  id: string
  nome: string
  enderecoIp: string
  ativo: boolean
  maquinaLinhaId: string
  maquina: string
  linhaId: string
  linha: string
  cliente: string
  ultimaMensagemEm: string | null
  coletaEmAndamento: boolean
  conectado: boolean
}

export interface SalvarDispositivoIotRequest {
  maquinaLinhaId: string
  nome: string
  enderecoIp: string
  ativo: boolean
}

export interface WiseDesconhecidoDto {
  enderecoIp: string
  clientId: string
  topico: string
  ultimaMensagemUtc: string
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

export interface SituacaoWiseDto {
  situacao: SituacaoConexaoWise
  dispositivoId: string | null
  nome: string | null
  enderecoIp: string | null
  ultimaMensagemUtc: string | null
  entradas: EntradaAoVivoDto[]
}

export const dispositivoIotService = {
  listar: () => api.get<DispositivoIotDto[]>('/dispositivos-iot'),
  desconhecidos: () => api.get<WiseDesconhecidoDto[]>('/dispositivos-iot/desconhecidos'),
  status: () => api.get<StatusColetaIotDto>('/dispositivos-iot/status'),
  criar: (dados: SalvarDispositivoIotRequest) => api.post<DispositivoIotDto>('/dispositivos-iot', dados),
  editar: (id: string, dados: SalvarDispositivoIotRequest) => api.put<DispositivoIotDto>(`/dispositivos-iot/${id}`, dados),
  excluir: (id: string) => api.delete<void>(`/dispositivos-iot/${id}`),

  // Validar entradas por IP: funciona com WISE cadastrado ou ainda sem máquina.
  entradasDoIp: (ip: string) => api.get<SituacaoWiseDto>(`/dispositivos-iot/entradas?ip=${encodeURIComponent(ip)}`),
}
