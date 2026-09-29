// Estado de edição das regras de classificação (Semi Automático) e conversões de/para a API.
import type { RegraDto, SalvarRegraRequest, TipoCondicao } from '../../services/regrasClassificacaoService'
import type { TextoEntrada } from '../../utils/entradasWise'

export interface CondicaoEdit {
  tipo: TipoCondicao
  canal: string
  // Só na condição de tempo: minutos, como digitado
  minutos: string
}

export interface RegraEdit {
  // Chave estável da lista (id da regra ou temporária)
  chave: string
  // Nulo = regra nova
  id: string | null
  nome: string
  motivoParadaId: string
  ativa: boolean
  condicoes: CondicaoEdit[]
}

// Motivo que pode ser escolhido numa regra
export interface MotivoOpcao {
  id: string
  nome: string
  tipo: string
  ativo: boolean
}

let contador = 0
export function chaveNova() {
  contador += 1
  return `nova-${Date.now()}-${contador}`
}

export function paraEdicao(regras: RegraDto[]): RegraEdit[] {
  return regras.map(r => ({
    chave: r.id,
    id: r.id,
    // Nome igual ao do motivo fica vazio: acompanha o motivo se ele for trocado
    nome: r.nome === r.motivo ? '' : r.nome,
    motivoParadaId: r.motivoParadaId,
    ativa: r.ativa,
    condicoes: r.condicoes.map(c => ({
      tipo: c.tipo,
      canal: c.canal ?? 'S8',
      minutos: c.tempoMinimoSegundos ? String(Math.round(c.tempoMinimoSegundos / 60)) : '',
    })),
  }))
}

export function paraSalvar(regras: RegraEdit[]): SalvarRegraRequest[] {
  return regras.map(r => ({
    id: r.id,
    nome: r.nome.trim() || null,
    motivoParadaId: r.motivoParadaId,
    ativa: r.ativa,
    condicoes: r.condicoes.map(c => c.tipo === 'TempoParadaMinimo'
      ? { tipo: c.tipo, canal: null, tempoMinimoSegundos: Math.round(Number(c.minutos) * 60) }
      : { tipo: c.tipo, canal: c.canal, tempoMinimoSegundos: null }),
  }))
}

// Mesmas regras do backend, para avisar antes de salvar. Nulo = tudo certo.
export function validarRegras(regras: RegraEdit[]): string | null {
  for (let i = 0; i < regras.length; i++) {
    const r = regras[i]
    const n = `Regra ${i + 1}`
    if (!r.motivoParadaId) return `${n}: escolha o motivo.`
    if (r.condicoes.length === 0) return `${n}: informe ao menos uma condição (sem condição, a regra classificaria qualquer parada).`
    for (const c of r.condicoes) {
      if (c.tipo === 'TempoParadaMinimo') {
        const m = Number(c.minutos)
        if (!Number.isInteger(m) || m < 1) return `${n}: o tempo da condição deve ser um número inteiro de minutos, maior que zero.`
      } else if (!c.canal) {
        return `${n}: escolha o sensor da condição.`
      }
    }
  }
  return null
}

// Como a condição aparece para as pessoas: "S8 - Falta de garrafas na entrada", "parada há 10 min ou mais".
export function textoCondicao(c: CondicaoEdit, textos: Record<string, TextoEntrada>): string {
  if (c.tipo === 'TempoParadaMinimo') return `parada há ${c.minutos || '?'} min ou mais`
  const t = textos[c.canal]
  const texto = c.tipo === 'SensorEmAlarme' ? t?.ativo : t?.normal
  return `${c.canal} - ${texto ?? t?.nome ?? ''}`
}
