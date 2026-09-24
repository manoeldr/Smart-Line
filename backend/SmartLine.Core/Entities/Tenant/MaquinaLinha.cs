using SmartLine.Core.Enums;
using SmartLine.Core.Entities.Global;

namespace SmartLine.Core.Entities.Tenant;

public class MaquinaLinha
{
    public Guid Id { get; set; }
    public Guid LinhaId { get; set; }
    public Guid MaquinaId { get; set; }
    public TipoColeta TipoColeta { get; set; }
    public decimal VelocidadeNominal { get; set; }
    public decimal SobreVelocidade { get; set; } = 0;
    public bool Critica { get; set; } = false;
    // Nem toda máquina conta unidades produzidas (ex: algumas etapas do processo não têm
    // contador físico). Quando false, a medição não pede/coleta leitura de Produção,
    // e Performance/OEE ficam indisponíveis (não dá pra calcular sem dado de produção).
    public bool MedeProducao { get; set; } = true;
    // Semi Automático: segundos sem incremento no contador de produção para considerar
    // a máquina parada (Z). Precisa ser maior que o filtro de 10 s dos sensores de estado
    // no WISE, senão a parada nasce sem motivo e é reclassificada segundos depois.
    public int TempoDeteccaoParadaSegundos { get; set; } = 60;
    public int Ordem { get; set; }
    public bool Ativo { get; set; } = true;
    // Navegação
    public Linha Linha { get; set; } = null!;
    public Maquina Maquina { get; set; } = null!;
    public ICollection<Sessao> Sessoes { get; set; } = [];
}