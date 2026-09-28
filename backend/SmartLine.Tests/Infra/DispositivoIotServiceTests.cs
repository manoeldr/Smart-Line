using SmartLine.Core.Entities.Global;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Interfaces;
using SmartLine.Infrastructure.Repositories;

namespace SmartLine.Tests.Infra;

public class DispositivoIotServiceTests : IDisposable
{
    private readonly BancoEmMemoria _banco = new();
    private readonly Guid _enchedora = Guid.NewGuid();
    private readonly Guid _rotuladora = Guid.NewGuid();
    private readonly Guid _usuario = Guid.NewGuid();

    public DispositivoIotServiceTests()
    {
        using var db = _banco.NovoContexto();
        var cliente = new Cliente { Id = Guid.NewGuid(), Nome = "Cervejaria" };
        var linha = new Linha { Id = Guid.NewGuid(), ClienteId = cliente.Id, Nome = "Linha 1" };
        var ench = new Maquina { Id = Guid.NewGuid(), Nome = "Enchedora", Ativo = true };
        var rot = new Maquina { Id = Guid.NewGuid(), Nome = "Rotuladora", Ativo = true };
        db.AddRange(cliente, linha, ench, rot,
            new Usuario { Id = _usuario, Nome = "Auditor", Login = "auditor", SenhaHash = "x", Nivel = NivelUsuario.Auditor },
            new MaquinaLinha { Id = _enchedora, LinhaId = linha.Id, MaquinaId = ench.Id, Ordem = 1, Ativo = true },
            new MaquinaLinha { Id = _rotuladora, LinhaId = linha.Id, MaquinaId = rot.Id, Ordem = 2, Ativo = true });
        db.SaveChanges();
    }

    public void Dispose() => _banco.Dispose();

    private async Task<T> Com<T>(Func<DispositivoIotService, Task<T>> acao)
    {
        await using var db = _banco.NovoContexto();
        return await acao(new DispositivoIotService(db));
    }

    private Task<ResultadoCadastro<DispositivoIotDto>> Criar(Guid maquina, string ip, string nome = "WISE") =>
        Com(s => s.CriarAsync(new SalvarDispositivoIotRequest(maquina, nome, ip)));

    private void LigarColeta(Guid maquina)
    {
        using var db = _banco.NovoContexto();
        db.Acompanhamentos.Add(new Acompanhamento
        {
            Id = Guid.NewGuid(), MaquinaLinhaId = maquina, UsuarioId = _usuario,
            IniciadoEm = DateTime.UtcNow, TempoDeteccaoParadaSegundos = 60
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Criar_GravaOIpNaFormaCanonica_EDevolveComNomes()
    {
        var r = await Criar(_enchedora, "  192.168.10.21 ", nome: " WISE Enchedora ");

        Assert.True(r.Sucesso);
        var d = r.Valor!;
        Assert.Equal(("WISE Enchedora", "192.168.10.21"), (d.Nome, d.EnderecoIp));
        Assert.Equal(("Enchedora", "Linha 1", "Cervejaria"), (d.Maquina, d.Linha, d.Cliente));
        Assert.False(d.ColetaEmAndamento);
    }

    [Theory]
    [InlineData("192.168.10")]
    [InlineData("192.168.010.21")]
    [InlineData("wise-enchedora")]
    [InlineData("fe80::1")]
    [InlineData("")]
    public async Task Criar_IpInvalido_Recusa(string ip)
    {
        var r = await Criar(_enchedora, ip);

        Assert.StartsWith("IP inválido", r.Erro);
    }

    [Fact]
    public async Task Criar_SemNome_Recusa() =>
        Assert.Equal("Informe o nome do WISE.", (await Criar(_enchedora, "192.168.10.21", nome: "  ")).Erro);

    [Fact]
    public async Task Criar_MaquinaInexistente_Recusa() =>
        Assert.Equal("Máquina não encontrada.", (await Criar(Guid.NewGuid(), "192.168.10.21")).Erro);

    [Fact]
    public async Task Criar_IpJaUsado_DizOndeEsta()
    {
        await Criar(_enchedora, "192.168.10.21", nome: "WISE Enchedora");

        var r = await Criar(_rotuladora, "192.168.10.21");

        Assert.Equal("O IP 192.168.10.21 já está cadastrado no WISE \"WISE Enchedora\" (Enchedora, Linha 1).", r.Erro);
    }

    [Fact]
    public async Task Criar_MaquinaQueJaTemWise_Recusa()
    {
        await Criar(_enchedora, "192.168.10.21", nome: "WISE Enchedora");

        var r = await Criar(_enchedora, "192.168.10.22");

        Assert.Contains("Cada máquina tem um WISE só", r.Erro);
    }

    [Fact]
    public async Task Editar_TrocaOIp_MesmoComColetaLigada()
    {
        var id = (await Criar(_enchedora, "192.168.10.21")).Valor!.Id;
        LigarColeta(_enchedora);

        var r = await Com(s => s.EditarAsync(id, new SalvarDispositivoIotRequest(_enchedora, "WISE", "192.168.10.99")));

        Assert.Equal(("192.168.10.99", true), (r.Valor!.EnderecoIp, r.Valor.ColetaEmAndamento));
    }

    [Fact]
    public async Task Editar_ProprioIpNaoContaComoRepetido()
    {
        var id = (await Criar(_enchedora, "192.168.10.21")).Valor!.Id;

        var r = await Com(s => s.EditarAsync(id, new SalvarDispositivoIotRequest(_enchedora, "Outro nome", "192.168.10.21")));

        Assert.Equal("Outro nome", r.Valor!.Nome);
    }

    [Fact]
    public async Task Editar_ComColetaLigada_NaoTrocaDeMaquinaNemDesativa()
    {
        var id = (await Criar(_enchedora, "192.168.10.21")).Valor!.Id;
        LigarColeta(_enchedora);

        var trocar = await Com(s => s.EditarAsync(id, new SalvarDispositivoIotRequest(_rotuladora, "WISE", "192.168.10.21")));
        var desativar = await Com(s => s.EditarAsync(id, new SalvarDispositivoIotRequest(_enchedora, "WISE", "192.168.10.21", Ativo: false)));

        Assert.Contains("Finalize a coleta", trocar.Erro);
        Assert.Contains("Finalize a coleta", desativar.Erro);
    }

    [Fact]
    public async Task Editar_Inexistente_NaoEncontrado() =>
        Assert.True((await Com(s => s.EditarAsync(Guid.NewGuid(), new SalvarDispositivoIotRequest(_enchedora, "WISE", "192.168.10.21")))).NaoEncontrado);

    [Fact]
    public async Task Excluir_ComColetaLigada_Recusa_SemColeta_Exclui()
    {
        var id = (await Criar(_enchedora, "192.168.10.21")).Valor!.Id;
        LigarColeta(_enchedora);
        Assert.Contains("Finalize a coleta", (await Com(s => s.ExcluirAsync(id))).Erro);

        var outro = (await Criar(_rotuladora, "192.168.10.22")).Valor!.Id;
        Assert.True((await Com(s => s.ExcluirAsync(outro))).Sucesso);

        var restantes = await Com(s => s.ListarAsync());
        Assert.Equal(id, Assert.Single(restantes).Id);
    }

    [Fact]
    public async Task Listar_NaOrdemDaLinha_ComColetaMarcada()
    {
        await Criar(_rotuladora, "192.168.10.22", nome: "WISE Rotuladora");
        await Criar(_enchedora, "192.168.10.21", nome: "WISE Enchedora");
        LigarColeta(_rotuladora);

        var lista = await Com(s => s.ListarAsync());

        Assert.Equal(new[] { ("WISE Enchedora", false), ("WISE Rotuladora", true) },
            lista.Select(d => (d.Nome, d.ColetaEmAndamento)));
    }
}
