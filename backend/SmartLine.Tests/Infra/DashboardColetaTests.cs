using SmartLine.Core.Services;
using SmartLine.Infrastructure.Repositories;
using static SmartLine.Tests.Infra.CenarioColeta;

namespace SmartLine.Tests.Infra;

/// <summary>Dashboard com a coleta Semi Automática em andamento (sessão do dia ainda aberta).</summary>
public class DashboardColetaTests : IDisposable
{
    private readonly CenarioColeta _c = new();

    public void Dispose() => _c.Dispose();

    [Fact]
    public async Task SessaoEmAndamento_EntraNoDashboard()
    {
        await _c.Consolidar(Em(1800), garrafas: 500, rejeito: 7);

        await using var db = _c.Banco.NovoContexto();
        var linhaId = db.MaquinasLinha.Single(m => m.Id == _c.MaquinaLinha).LinhaId;
        var maquina = Assert.Single(await new DashboardService(db, new OeeService())
            .GetDashboardLinhaAsync(linhaId, T0.UtcDateTime.AddHours(-1), T0.UtcDateTime.AddDays(1)));

        Assert.Equal((1, 500, 7), (maquina.NumSessoes, maquina.Producao, maquina.Refugo));
    }
}
