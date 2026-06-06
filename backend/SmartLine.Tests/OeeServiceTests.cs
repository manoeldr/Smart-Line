using SmartLine.Core.Entities.Global;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Enums;
using SmartLine.Core.Services;

namespace SmartLine.Tests;

public class OeeServiceTests
{
    private readonly OeeService _sut = new();

    private static Sessao CriarSessao(DateTime inicio, DateTime fim)
    {
        return new Sessao
        {
            Id = Guid.NewGuid(),
            Inicio = inicio,
            Fim = fim,
            Status = StatusSessao.Finalizada,
            Paradas = [],
            Producoes = []
        };
    }

    private static Parada CriarParada(DateTime inicio, DateTime fim, TipoParada tipo)
    {
        return new Parada
        {
            Id = Guid.NewGuid(),
            Inicio = inicio,
            Fim = fim,
            Motivo = new MotivoParada { Nome = tipo.ToString(), Tipo = tipo }
        };
    }

    // ── Disponibilidade ───────────────────────────────────────────

    [Fact]
    public void Disponibilidade_SemParadas_DeveSerCem()
    {
        var inicio = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var fim = inicio.AddHours(1);
        var sessao = CriarSessao(inicio, fim);

        var resultado = _sut.Calcular(sessao, 1000);

        Assert.Equal(100.0, resultado.Disponibilidade);
    }

    [Fact]
    public void Disponibilidade_ComParadaInterna_DeveSerCinquenta()
    {
        var inicio = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var fim = inicio.AddHours(2);
        var sessao = CriarSessao(inicio, fim);

        sessao.Paradas = [
            CriarParada(inicio.AddHours(1), fim, TipoParada.Interna)
        ];

        var resultado = _sut.Calcular(sessao, 1000);

        Assert.Equal(50.0, resultado.Disponibilidade);
    }

    [Fact]
    public void Disponibilidade_ComParadaExterna_DeveSerCem()
    {
        var inicio = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var fim = inicio.AddHours(2);
        var sessao = CriarSessao(inicio, fim);

        sessao.Paradas = [
            CriarParada(inicio.AddHours(1), fim, TipoParada.Externa)
        ];

        var resultado = _sut.Calcular(sessao, 1000);

        Assert.Equal(100.0, resultado.Disponibilidade);
    }

    [Fact]
    public void Disponibilidade_ComParadaPlanejada_DeveDescontarDoTempo()
    {
        var inicio = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var fim = inicio.AddHours(2);
        var sessao = CriarSessao(inicio, fim);

        // 30 min planejada + 30 min interna
        sessao.Paradas = [
            CriarParada(inicio, inicio.AddMinutes(30), TipoParada.Planejada),
            CriarParada(inicio.AddHours(1), inicio.AddHours(1).AddMinutes(30), TipoParada.Interna)
        ];

        // Tempo total = 2h, planejada = 30min, disponível = 90min, interna = 30min
        // Disponibilidade = (90 - 30) / 90 * 100 = 66.7%
        var resultado = _sut.Calcular(sessao, 1000);

        Assert.Equal(66.7, resultado.Disponibilidade);
    }

    // ── Qualidade ─────────────────────────────────────────────────

    [Fact]
    public void Qualidade_SemRefugo_DeveSerCem()
    {
        var inicio = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var sessao = CriarSessao(inicio, inicio.AddHours(1));

        sessao.Producoes = [
            new Producao { Quantidade = 500, Refugo = 0 }
        ];

        var resultado = _sut.Calcular(sessao, 1000);

        Assert.Equal(100.0, resultado.Qualidade);
    }

    [Fact]
    public void Qualidade_ComRefugo_DeveCalcularCorretamente()
    {
        var inicio = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var sessao = CriarSessao(inicio, inicio.AddHours(1));

        sessao.Producoes = [
            new Producao { Quantidade = 900, Refugo = 100 }
        ];

        // Qualidade = (900 - 100) / 900 * 100 = 88.9%
        var resultado = _sut.Calcular(sessao, 1000);

        Assert.Equal(88.9, resultado.Qualidade);
    }

    // ── OEE ───────────────────────────────────────────────────────

    [Fact]
    public void Oee_SemParadasSemRefugo_DeveSerIgualPerformance()
    {
        var inicio = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var fim = inicio.AddHours(1);
        var sessao = CriarSessao(inicio, fim);

        // 1h rodando, velocidade 1000/h, produção 800
        sessao.Producoes = [new Producao { Quantidade = 800, Refugo = 0 }];

        var resultado = _sut.Calcular(sessao, 1000);

        // Disponibilidade=100, Performance=80, Qualidade=100 → OEE=80
        Assert.Equal(100.0, resultado.Disponibilidade);
        Assert.Equal(80.0, resultado.Performance);
        Assert.Equal(100.0, resultado.Qualidade);
        Assert.Equal(80.0, resultado.Oee);
    }
}
