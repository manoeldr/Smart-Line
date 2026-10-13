// Seção 1 do relatório — dados da medição: quem, onde, o quê e quando foi medido.
// Produto e Responsável do Cliente são digitados na tela; o resto vem da sessão da máquina
// de referência (ver referencia.ts). Tempo total é bruto: fim − início, sem descontar pausas.
import { dataHoraLonga, duracaoHoras, inteiro } from './formatos'

export interface DadosMedicao {
  clienteNome: string
  linhaNome: string
  produto: string
  velocidadeNominal: number | null
  responsavelMedicao: string
  responsavelCliente: string
  inicio: string | null
  fim: string | null
  emAndamento: boolean
}

function Campo({ rotulo, valor, vazio }: { rotulo: string; valor: string; vazio?: boolean }) {
  return (
    <div className="flex flex-col gap-0.5 py-2 border-b border-borda-suave break-inside-avoid">
      <span className="text-[10px] uppercase tracking-wider text-texto-3">{rotulo}</span>
      <span className={`text-sm ${vazio ? 'text-amber-600 italic' : 'text-texto'}`}>{valor}</span>
    </div>
  )
}

// agora: referência do "até agora" quando a medição ainda está em andamento
export default function SecaoDadosMedicao({ dados, agora }: { dados: DadosMedicao; agora: Date }) {
  const fimMs = dados.fim ? new Date(dados.fim).getTime() : agora.getTime()
  const tempoTotal = dados.inicio ? duracaoHoras(fimMs - new Date(dados.inicio).getTime()) : '—'

  return (
    <section className="break-inside-avoid">
      <h2 className="text-xs font-semibold uppercase tracking-widest text-texto-2 border-b-2 border-texto pb-1 mb-2">
        1. Dados da medição
      </h2>
      <div className="grid grid-cols-2 gap-x-8">
        <Campo rotulo="Cliente" valor={dados.clienteNome} />
        <Campo rotulo="Linha" valor={dados.linhaNome} />
        <Campo rotulo="Produto" valor={dados.produto || 'não informado'} vazio={!dados.produto} />
        <Campo
          rotulo="Velocidade nominal da linha"
          valor={dados.velocidadeNominal != null ? `${inteiro(dados.velocidadeNominal)} garrafas/h` : '—'}
        />
        <Campo rotulo="Responsável pela medição" valor={dados.responsavelMedicao} />
        <Campo rotulo="Responsável do cliente" valor={dados.responsavelCliente || 'não informado'} vazio={!dados.responsavelCliente} />
        <Campo rotulo="Início da medição" valor={dataHoraLonga(dados.inicio)} />
        <Campo rotulo="Fim da medição" valor={dados.emAndamento ? 'em andamento' : dataHoraLonga(dados.fim)} />
        <Campo rotulo="Tempo total de medição" valor={dados.emAndamento ? `${tempoTotal} (até agora)` : tempoTotal} />
      </div>
    </section>
  )
}
