export type NivelUsuario = 'SuperAdmin' | 'Auditor' | 'Visualizador'
export type TipoColeta = 'Manual' | 'SemiAutomatico'
export type TipoParada = 'Interna' | 'Externa' | 'Planejada'
export type StatusSessao = 'EmAndamento' | 'Finalizada'

export type StatusMaquina = 'Rodando' | 'ParadaInterna' | 'ParadaExterna' | 'ParadaPlanejada' | 'SemSessao'

export interface Cliente {
  id: string
  nome: string
  estado: string | null
  ativo: boolean
}

export interface Usuario {
  id: string
  clienteId: string | null
  nome: string
  login: string
  nivel: NivelUsuario
  ativo: boolean
}

export interface Maquina {
  id: string
  nome: string
  fabricante: string | null
  descricao: string | null
  ativo: boolean
}

export interface MaquinaLinha {
  id: string
  linhaId: string
  maquinaId: string
  maquinaNome: string
  tipoColeta: TipoColeta
  velocidadeNominal: number
  critica: boolean
  ordem: number
  ativo: boolean
  status: StatusMaquina
  oee: number | null
  sessaoAtiva: boolean
}

export interface Linha {
  id: string
  clienteId: string
  nome: string
  ativo: boolean
  maquinas: MaquinaLinha[]
}

export interface Sessao {
  id: string
  maquinaLinhaId: string
  usuarioId: string
  inicio: string
  fim: string | null
  status: StatusSessao
}

export interface OeeResultado {
  tempoTotalMs: number
  tempoPlanejadoMs: number
  tempoDisponivelMs: number
  tempoInternoMs: number
  tempoExternoMs: number
  tempoRodandoMs: number
  disponibilidade: number
  performance: number
  qualidade: number
  oee: number
  producao: number
  refugo: number
  numParadas: number
  numParadasInternas: number
  numParadasExternas: number
  numParadasPlanejadas: number
}
