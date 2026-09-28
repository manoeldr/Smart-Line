using SmartLine.Core.Coleta;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;
using static SmartLine.Tests.Infra.CenarioColeta;

namespace SmartLine.Tests.Infra;

/// <summary>O que o painel da coleta lê do banco.</summary>
public class ColetasEmAndamentoTests : IDisposable
{
    private readonly CenarioColeta _c = new();

    public void Dispose() => _c.Dispose();

    private async Task<IReadOnlyList<ColetaIotResumoDto>> Listar(Guid? maquina = null)
    {
        await using var db = _c.Banco.NovoContexto();
        return await _c.Servico(db).ListarEmAndamentoAsync(maquina);
    }

    [Fact]
    public async Task RecemIniciada_TrazQuemOndeEOQueLe()
    {
        var r = Assert.Single(await Listar());

        Assert.Equal((_c.Acompanhamento, _c.MaquinaLinha, "Enchedora", "Linha 1", "Cliente"),
            (r.AcompanhamentoId, r.MaquinaLinhaId, r.Maquina, r.Linha, r.Cliente));
        Assert.Equal((_c.Usuario, "Auditor", T0.UtcDateTime, 60), (r.UsuarioId, r.Usuario, r.IniciadoEm, r.TempoDeteccaoParadaSegundos));
        Assert.Equal(new[] { CanalWise.S1, CanalWise.S2, CanalWise.S3, CanalWise.S4, CanalWise.S7, CanalWise.S8 },
            r.Canais.Select(c => c.Canal));
        Assert.Equal(("192.168.10.21", _c.Sessao, T0.UtcDateTime, 36000m), (r.EnderecoIp, r.SessaoId, r.SessaoInicio, r.VelocidadeNominal));
        Assert.Equal((0L, 0L), (r.ProducaoConsolidada, r.RefugoConsolidado));
        Assert.Null(r.ParadaAberta);
        Assert.Null(r.SemComunicacaoDesde);
        Assert.Equal(0, r.ParadasNaoClassificadas);
    }

    [Fact]
    public async Task ProducaoParadaEComunicacao_VemDoQueFoiGravado()
    {
        await _c.Consolidar(Em(300), garrafas: 1200, rejeito: 5);
        await _c.Registrar(new ParadaIniciada(Em(360), _c.Classificacao("Falta de garrafas na entrada")));
        await _c.Registrar(new ComunicacaoPerdida(Em(400)));

        var r = Assert.Single(await Listar());

        Assert.Equal((1200L, 5L, Em(300)), (r.ProducaoConsolidada, r.RefugoConsolidado, r.UltimaConsolidacao!.Value));
        Assert.Equal(("Falta de garrafas na entrada", TipoParada.Externa, Em(360)),
            (r.ParadaAberta!.Motivo, r.ParadaAberta.Tipo, r.ParadaAberta.Inicio));
        Assert.Equal(Em(400), r.SemComunicacaoDesde);
    }

    [Fact]
    public async Task ParadaSemCausa_ApareceComoInternaSemMotivo()
    {
        await _c.Registrar(new ParadaIniciada(Em(60), ClassificacaoParada.NaoClassificada));

        var r = Assert.Single(await Listar());
        var parada = r.ParadaAberta!;
        Assert.Equal(1, r.ParadasNaoClassificadas);

        Assert.Equal(((Guid?)null, (string?)null, TipoParada.Interna), (parada.MotivoId, parada.Motivo, parada.Tipo));
    }

    [Fact]
    public async Task FiltraPorMaquina_ESomeDepoisDeFinalizada()
    {
        Assert.Empty(await Listar(Guid.NewGuid()));
        Assert.Single(await Listar(_c.MaquinaLinha));

        await using (var db = _c.Banco.NovoContexto())
            await _c.Servico(db).FinalizarAsync(_c.Acompanhamento, _c.Usuario, false);

        Assert.Empty(await Listar());
    }
}
