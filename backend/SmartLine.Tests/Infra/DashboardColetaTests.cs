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

        Assert.Equal((500, 7, (DateTime?)T0.UtcDateTime), (maquina.Producao, maquina.Refugo, maquina.SessaoInicio));
        Assert.Equal((true, "Rodando", _c.Acompanhamento.ToString()), (maquina.AoVivo, maquina.SituacaoAoVivo, maquina.AcompanhamentoId));
    }

    [Fact]
    public async Task MostraOsValoresDaSessaoEmAndamento_NaoAMediaComAsAnteriores()
    {
        await _c.Consolidar(Em(1800), garrafas: 500);
        using (var db = _c.Banco.NovoContexto())
        {
            // Sessão Manual finalizada antes, no mesmo período, com outra produção.
            var anterior = new SmartLine.Core.Entities.Tenant.Sessao
            {
                Id = Guid.NewGuid(), MaquinaLinhaId = _c.MaquinaLinha, UsuarioId = _c.Usuario,
                Inicio = Em(-7200), Fim = Em(-3600), Status = SmartLine.Core.Enums.StatusSessao.Finalizada,
                VelocidadeNominal = 36000, CriadoEm = Em(-7200)
            };
            anterior.Producoes.Add(new SmartLine.Core.Entities.Tenant.Producao { Id = Guid.NewGuid(), Quantidade = 0, Hora = Em(-7200) });
            anterior.Producoes.Add(new SmartLine.Core.Entities.Tenant.Producao { Id = Guid.NewGuid(), Quantidade = 9000, Hora = Em(-3600) });
            db.Sessoes.Add(anterior);
            db.SaveChanges();
        }

        await using var ctx = _c.Banco.NovoContexto();
        var linhaId = ctx.MaquinasLinha.Single(m => m.Id == _c.MaquinaLinha).LinhaId;
        var maquina = Assert.Single(await new DashboardService(ctx, new OeeService())
            .GetDashboardLinhaAsync(linhaId, T0.UtcDateTime.AddHours(-3), T0.UtcDateTime.AddDays(1)));
        var detalhe = (await new SessaoDetalheService(ctx, new OeeService()).GetUltimaSessaoDetalheAsync(_c.MaquinaLinha))!;

        Assert.Equal((500, true), (maquina.Producao, maquina.AoVivo));
        Assert.Equal((detalhe.Oee, detalhe.Disponibilidade, detalhe.Qualidade, detalhe.Producao),
            (maquina.Oee, maquina.Disponibilidade, maquina.Qualidade, maquina.Producao));
    }

    [Fact]
    public async Task ParadaAberta_AoVivoParada_EDepoisDeFinalizarNaoEMaisAoVivo()
    {
        await _c.Registrar(new SmartLine.Core.Coleta.ParadaIniciada(Em(600), SmartLine.Core.Coleta.ClassificacaoParada.NaoClassificada));

        Assert.Equal((true, "Parada"), await AoVivo());

        await using (var db = _c.Banco.NovoContexto())
            await _c.Servico(db).FinalizarAsync(_c.Acompanhamento, _c.Usuario, false);

        Assert.Equal((false, (string?)null), await AoVivo());
    }

    private async Task<(bool, string?)> AoVivo()
    {
        await using var db = _c.Banco.NovoContexto();
        var linhaId = db.MaquinasLinha.Single(m => m.Id == _c.MaquinaLinha).LinhaId;
        var m = Assert.Single(await new DashboardService(db, new OeeService())
            .GetDashboardLinhaAsync(linhaId, T0.UtcDateTime.AddHours(-1), T0.UtcDateTime.AddDays(1)));
        return (m.AoVivo, m.SituacaoAoVivo);
    }
}
