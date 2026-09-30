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

    [Fact]
    public async Task VisaoDaLinha_OeePelaCritica_ParadasERefugoSomadosDeTodasAsMaquinas()
    {
        var falta = _c.Classificacao("Falta de garrafas na entrada");
        await _c.Registrar(new SmartLine.Core.Coleta.ParadaIniciada(Em(600), falta), new SmartLine.Core.Coleta.ParadaEncerrada(Em(900))); // 5 min
        await _c.Consolidar(Em(1800), garrafas: 500, rejeito: 7);

        var outra = Guid.NewGuid();
        using (var db = _c.Banco.NovoContexto())
        {
            var enchedora = db.MaquinasLinha.Single(m => m.Id == _c.MaquinaLinha);
            enchedora.Critica = true;
            db.MaquinasLinha.Add(new SmartLine.Core.Entities.Tenant.MaquinaLinha
            {
                Id = outra, LinhaId = enchedora.LinhaId, MaquinaId = _c.MaquinaCatalogo, VelocidadeNominal = 36000, Ativo = true, Ordem = 2
            });
            // Sessão Manual finalizada na outra máquina: 10 min de falta de garrafas, 30 de refugo.
            var sessao = new SmartLine.Core.Entities.Tenant.Sessao
            {
                Id = Guid.NewGuid(), MaquinaLinhaId = outra, UsuarioId = _c.Usuario,
                Inicio = Em(-7200), Fim = Em(-3600), Status = SmartLine.Core.Enums.StatusSessao.Finalizada,
                VelocidadeNominal = 36000, CriadoEm = Em(-7200)
            };
            sessao.Producoes.Add(new SmartLine.Core.Entities.Tenant.Producao { Id = Guid.NewGuid(), Quantidade = 0, Hora = Em(-7200) });
            sessao.Producoes.Add(new SmartLine.Core.Entities.Tenant.Producao { Id = Guid.NewGuid(), Quantidade = 9000, Refugo = 30, Hora = Em(-3600) });
            sessao.Paradas.Add(new SmartLine.Core.Entities.Tenant.Parada { Id = Guid.NewGuid(), Inicio = Em(-5400), Fim = Em(-4800), MotivoId = falta.MotivoParadaId });
            db.Sessoes.Add(sessao);
            db.SaveChanges();
        }

        await using var ctx = _c.Banco.NovoContexto();
        var linhaId = ctx.MaquinasLinha.Single(m => m.Id == _c.MaquinaLinha).LinhaId;
        var linha = await new DashboardService(ctx, new OeeService())
            .GetLinhaGeralAsync(linhaId, T0.UtcDateTime.AddHours(-3), T0.UtcDateTime.AddDays(1));
        var detalhe = (await new SessaoDetalheService(ctx, new OeeService()).GetUltimaSessaoDetalheAsync(_c.MaquinaLinha))!;

        // OEE e produção da máquina crítica
        Assert.True(linha.ReferenciaCritica);
        Assert.Equal((detalhe.Oee, detalhe.Disponibilidade, detalhe.Qualidade, 500), (linha.Oee, linha.Disponibilidade, linha.Qualidade, linha.Producao));
        Assert.Equal(_c.MaquinaLinha.ToString(), Assert.Single(linha.Maquinas, m => m.Referencia).MaquinaLinhaId);

        // Refugo e paradas somados das duas
        Assert.Equal((37, 15d, 2), (linha.RefugoTotal, linha.TempoParadoTotalMs / 60000, linha.NumParadas));
        var motivo = Assert.Single(linha.ParadasPorMotivo);
        Assert.Equal(("Falta de garrafas na entrada", 15d, 2), (motivo.Motivo, motivo.DuracaoMs / 60000, motivo.Quantidade));
        Assert.Equal(new[] { 5d, 10d }, motivo.PorMaquina.Select(p => p.DuracaoMs / 60000).Order());
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
