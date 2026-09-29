// Situação de um WISE (pelo IP) a partir da lista de WISE que o SmartLine conhece, e validação de IP.
import type { WiseDto } from '../../services/dispositivoIotService'
import type { SituacaoWise } from './SituacaoWiseTexto'

// IPv4 com quatro números de 0 a 255, sem zero à esquerda (a mesma regra do backend:
// "192.168.010.021" seria lido como octal e viraria outro IP).
export function ipValido(texto: string) {
  const partes = texto.trim().split('.')
  return partes.length === 4 && partes.every(p => /^(0|[1-9]\d{0,2})$/.test(p) && Number(p) <= 255)
}

export interface SituacaoDoIp {
  situacao: SituacaoWise
  // O WISE da lista com este IP; nulo se o SmartLine não sabe nada dele (nunca conectou)
  wise: WiseDto | null
}

// Em uso vale mais que a conexão: o WISE de outra medição não pode ser usado, conectado ou não.
export function situacaoDoIp(ip: string, wises: WiseDto[]): SituacaoDoIp {
  const wise = wises.find(w => w.enderecoIp === ip.trim()) ?? null
  const situacao: SituacaoWise = wise?.medicao ? 'EmUso' : wise?.conectado ? 'Conectado' : 'Desconectado'
  return { situacao, wise }
}
