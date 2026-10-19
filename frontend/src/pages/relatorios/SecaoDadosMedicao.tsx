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

// Cada informação num quadro próprio: rótulo em cima, valor embaixo
function Item({ rotulo, valor, vazio }: { rotulo: string; valor: string; vazio?: boolean }) {
  return (
    <div className="min-w-0 border border-borda bg-superficie-2 px-2 py-1.5">
      <p className="text-[10pt] text-texto-3 leading-tight whitespace-nowrap overflow-hidden">{rotulo}</p>
      <p className={`text-[10pt] leading-snug ${vazio ? 'text-amber-600 italic' : 'text-texto font-medium'}`}>{valor}</p>
    </div>
  )
}

// Uma linha de quadros: grade de 3 colunas iguais (alinhadas entre as linhas)
function Linha({ children }: { children: React.ReactNode }) {
  return <div className="grid grid-cols-3 gap-2">{children}</div>
}

// agora: referência do "até agora" quando a medição ainda está em andamento
export default function SecaoDadosMedicao({ dados, agora }: { dados: DadosMedicao; agora: Date }) {
  const fimMs = dados.fim ? new Date(dados.fim).getTime() : agora.getTime()
  const tempoTotal = dados.inicio ? duracaoHoras(fimMs - new Date(dados.inicio).getTime()) : '—'

  return (
    <section className="break-inside-avoid">
      <h2 className="text-[14pt] font-semibold uppercase tracking-wider text-texto-2 border-b-2 border-texto pb-1 mb-3">
        Dados da medição
      </h2>
      <div className="flex flex-col gap-2">
      <Linha>
        <Item rotulo="Cliente" valor={dados.clienteNome} />
        <Item rotulo="Linha" valor={dados.linhaNome} />
        <Item rotulo="Produto" valor={dados.produto || 'não informado'} vazio={!dados.produto} />
      </Linha>
      <Linha>
        <Item rotulo="Início" valor={dataHoraLonga(dados.inicio)} />
        <Item rotulo="Fim" valor={dados.emAndamento ? 'em andamento' : dataHoraLonga(dados.fim)} />
        <Item rotulo="Tempo total" valor={dados.emAndamento ? `${tempoTotal} (até agora)` : tempoTotal} />
      </Linha>
      <Linha>
        <Item
          rotulo="Velocidade nominal"
          valor={dados.velocidadeNominal != null ? `${inteiro(dados.velocidadeNominal)} garrafas/h` : '—'}
        />
        <Item rotulo="Responsável pela medição" valor={dados.responsavelMedicao} />
        <Item rotulo="Responsável do cliente" valor={dados.responsavelCliente || 'não informado'} vazio={!dados.responsavelCliente} />
      </Linha>
      </div>
    </section>
  )
}
