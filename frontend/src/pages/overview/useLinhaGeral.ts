// Visão geral da linha (a mesma da visão Linha do Dashboard) para os blocos do Overview:
// sessão em andamento de cada máquina, ou a do dia escolhido no filtro de data.
import { useEffect, useState } from 'react'
import { dashboardService, type LinhaDashboardDto } from '../../services/dashboardService'

export function useLinhaGeral(linhaId: string, dataFiltro: string | null, versao: number): LinhaDashboardDto | null | undefined {
  // undefined = carregando; null = falhou
  const [dados, setDados] = useState<LinhaDashboardDto | null | undefined>(undefined)
  useEffect(() => {
    if (!linhaId) return
    let ativo = true
    const dia = dataFiltro ?? new Date().toISOString().slice(0, 10)
    const inicio = new Date(dia + 'T00:00:00').toISOString()
    const fim = new Date(dia + 'T23:59:59').toISOString()
    dashboardService.getLinhaGeral(linhaId, inicio, fim)
      .then(d => { if (ativo) setDados(d) })
      .catch(() => { if (ativo) setDados(null) })
    return () => { ativo = false }
  }, [linhaId, dataFiltro, versao])
  return dados
}
