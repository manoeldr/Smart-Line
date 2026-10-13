// Folha timbrada do relatório: situação (margens, versão) e a imagem como URL de blob.
// Usado na aba Relatório das Configurações e na tela de Relatórios.
import { useCallback, useEffect, useState } from 'react'
import { modeloRelatorioService, type ModeloRelatorioDto } from '../services/modeloRelatorioService'

export function useModeloRelatorio() {
  const [modelo, setModelo] = useState<ModeloRelatorioDto | null>(null)
  // A imagem baixada e de qual versão ela é (versão diferente da atual = ainda não baixou)
  const [imagem, setImagem] = useState<{ versao: number; url: string } | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  const recarregar = useCallback(async () => {
    try {
      const m = await modeloRelatorioService.obter()
      setModelo(m)
      setErro(null)
    } catch (e: unknown) {
      setErro(e instanceof Error ? e.message : 'Erro ao carregar a folha do relatório')
    }
  }, [])

  useEffect(() => {
    let ativo = true
    modeloRelatorioService.obter()
      .then(m => { if (ativo) { setModelo(m); setErro(null) } })
      .catch(e => { if (ativo) setErro(e instanceof Error ? e.message : 'Erro ao carregar a folha do relatório') })
    return () => { ativo = false }
  }, [])

  // Baixa a imagem quando a versão muda; libera a URL ao trocar ou desmontar.
  const existe = modelo?.existe ?? false
  const versao = modelo?.versao ?? 0
  useEffect(() => {
    if (!existe) return
    let ativo = true
    let url: string | null = null
    modeloRelatorioService.carregarImagem(versao)
      .then(u => {
        url = u
        if (ativo && u) setImagem({ versao, url: u })
        else if (u) URL.revokeObjectURL(u)
      })
      .catch(() => { /* sem imagem: o relatório sai sem timbre */ })
    return () => {
      ativo = false
      if (url) URL.revokeObjectURL(url)
    }
  }, [existe, versao])

  const imagemUrl = existe && imagem?.versao === versao ? imagem.url : null
  return { modelo, setModelo, imagemUrl, erro, recarregar }
}
