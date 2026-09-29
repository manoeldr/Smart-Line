// Textos padrão das entradas do WISE (os mesmos do backend). Cada máquina do catálogo pode
// personalizar os seus; estes servem de ponto de partida e para "restaurar padrão".

export interface TextoEntrada {
  nome: string
  // Só sensores de estado
  ativo: string | null
  normal: string | null
}

export const SENSORES_WISE = ['S1', 'S4', 'S7', 'S8']
export const CANAIS_WISE = ['S1', 'S2', 'S3', 'S4', 'S5', 'S6', 'S7', 'S8']

export const TEXTOS_PADRAO_WISE: Record<string, TextoEntrada> = {
  S1: { nome: 'Acúmulo mínimo na entrada', ativo: 'Abaixo do acúmulo mínimo', normal: 'Acúmulo mínimo normal' },
  S2: { nome: 'Contador de produção (entrada 1)', ativo: null, normal: null },
  S3: { nome: 'Contador de rejeito', ativo: null, normal: null },
  S4: { nome: 'Acúmulo na saída (caixas/pallets)', ativo: 'Saída de caixas/pallets bloqueada', normal: 'Saída de caixas/pallets livre' },
  S5: { nome: 'Contador de produção (entrada 2)', ativo: null, normal: null },
  S6: { nome: 'Contador de produção (entrada 3)', ativo: null, normal: null },
  S7: { nome: 'Acúmulo na saída de garrafas', ativo: 'Saída de garrafas bloqueada', normal: 'Saída de garrafas livre' },
  S8: { nome: 'Falta de garrafas na entrada', ativo: 'Falta de garrafas na entrada', normal: 'Com garrafas na entrada' },
}

export const TAMANHO_MAXIMO_TEXTO = 60
