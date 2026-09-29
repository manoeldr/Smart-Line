// Editar o motivo de uma parada (Manual ou Semi Automático). Reaproveita o modal de motivos
// da Medição, inclusive o "Cadastrar novo motivo" na hora (fica na máquina do catálogo e já
// é aplicado à parada). A troca vai para o histórico com o usuário logado.
import { useEffect, useState } from 'react'
import { maquinaService, type MotivoParadaDto } from '../services/maquinaService'
import { paradaColetaService } from '../services/paradaColetaService'
import { mensagemErro } from '../services/api'
import MotivoParadaModal from './MotivoParadaModal'

interface Props {
  // Parada a editar e a máquina do catálogo dela (de onde vêm os motivos); nulo = fechado
  alvo: { paradaId: string; maquinaId: string } | null
  onFechar: () => void
  onSalvo: () => void
}

export default function EditarMotivoParadaModal({ alvo, onFechar, onSalvo }: Props) {
  // Montado só quando aberto: cada parada começa com a lista de motivos recarregada.
  return alvo ? <Conteudo key={alvo.paradaId} alvo={alvo} onFechar={onFechar} onSalvo={onSalvo} /> : null
}

function Conteudo({ alvo, onFechar, onSalvo }: { alvo: { paradaId: string; maquinaId: string }; onFechar: () => void; onSalvo: () => void }) {
  const [motivos, setMotivos] = useState<MotivoParadaDto[]>([])
  const [carregando, setCarregando] = useState(true)

  useEffect(() => {
    let ativo = true
    maquinaService.getMotivosParada(alvo.maquinaId)
      .then(m => { if (ativo) setMotivos(m) })
      .catch(e => { if (ativo) alert(mensagemErro(e, 'Erro ao carregar os motivos da máquina.')) })
      .finally(() => { if (ativo) setCarregando(false) })
    return () => { ativo = false }
  }, [alvo.maquinaId])

  async function confirmar(motivoId: string) {
    try {
      await paradaColetaService.reclassificar(alvo.paradaId, motivoId)
      onSalvo()
    } catch (e) {
      alert(mensagemErro(e, 'Não foi possível trocar o motivo da parada.'))
    }
  }

  // Motivo novo fica na máquina do catálogo (vale para as outras máquinas do mesmo tipo).
  async function cadastrarNovo(nome: string, tipo: 'Interna' | 'Externa') {
    try {
      const novo = await maquinaService.criarMotivoParada(alvo.maquinaId, nome, tipo)
      setMotivos(m => [...m, novo])
      return novo.id
    } catch (e) {
      alert(mensagemErro(e, 'Não foi possível cadastrar o motivo.'))
      return ''
    }
  }

  return (
    <MotivoParadaModal
      open
      motivos={motivos}
      loading={carregando}
      onConfirmar={confirmar}
      onCadastrarNovo={cadastrarNovo}
      onCancelar={onFechar}
    />
  )
}
