// "Máquina de referência" do relatório: a que dita velocidade nominal, datas e tempo total.
// Crítica medida no dia; senão a única medida; senão, entre as medidas que medem produção,
// a de pior OEE (mesma regra da visão Linha do Dashboard, para os números baterem).
import type { Linha, MaquinaLinha } from '../../types'

export function maquinaDeReferencia(linha: Linha | null | undefined): MaquinaLinha | null {
  if (!linha) return null
  const medidas = linha.maquinas.filter(m => m.ativo && m.sessaoDia)
  if (medidas.length === 0) return null
  const critica = medidas.find(m => m.critica)
  if (critica) return critica
  if (medidas.length === 1) return medidas[0]
  const comProducao = medidas.filter(m => m.medeProducao)
  const candidatas = comProducao.length > 0 ? comProducao : medidas
  return candidatas.reduce((pior, m) => ((m.oee ?? Infinity) < (pior.oee ?? Infinity) ? m : pior))
}
