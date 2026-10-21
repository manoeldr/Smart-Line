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

    // A produção do turno é a última leitura menos a inicial (mesmo cálculo do sistema
    // legado): toda sessão real começa com a leitura de "produção até então". Hora
    // explícita porque o default (UtcNow) deixaria a ordem das leituras ao acaso.
    private static List<Producao> Leituras(DateTime inicio, int quantidadeFinal, int refugoFinal) =>
    [
        new Producao { Quantidade = 0, Refugo = 0, Hora = inicio },
        new Producao { Quantidade = quantidadeFinal, Refugo = refugoFinal, Hora = inicio.AddHours(1) }
    ];

    private static Parada CriarParadaSemMotivo(DateTime inicio, DateTime fim)
    {
        return new Parada { Id = Guid.NewGuid(), Inicio = inicio, Fim = fim, Motivo = null };
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
    public void Disponibilidade_ComParadaExterna_DescontaComoAInterna()
    {
        // Desde 21/10 a parada externa sai do tempo rodando (OEE padrão): 2 h, 1 h externa → 50 %.
        var inicio = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var fim = inicio.AddHours(2);
        var sessao = CriarSessao(inicio, fim);

        sessao.Paradas = [
            CriarParada(inicio.AddHours(1), fim, TipoParada.Externa)
        ];

        var resultado = _sut.Calcular(sessao, 1000);

        Assert.Equal(50.0, resultado.Disponibilidade);
        Assert.Equal(3_600_000, resultado.TempoRodandoMs);
        Assert.Equal(3_600_000, resultado.TempoExternoMs);
    }

    [Fact]
    public void Fatias_SomamOTempoTotal()
    {
        // Total 6 h = rodando 2 h + interna 1 h + externa 1 h + programada 1 h + sem comunicação 1 h.
        var inicio = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var fim = inicio.AddHours(6);
        var sessao = CriarSessao(inicio, fim);
        sessao.Paradas = [
            CriarParada(inicio.AddHours(1), inicio.AddHours(2), TipoParada.Interna),
            CriarParada(inicio.AddHours(2), inicio.AddHours(3), TipoParada.Externa),
            CriarParada(inicio.AddHours(3), inicio.AddHours(4), TipoParada.Planejada),
        ];
        var semComunicacao = new PeriodoSemComunicacao { Inicio = inicio.AddHours(4), Fim = inicio.AddHours(5) };

        var r = _sut.Calcular(sessao, 1000, true, [semComunicacao]);

        Assert.Equal(6 * 3_600_000d, r.TempoTotalMs);
        Assert.Equal(r.TempoTotalMs, r.TempoRodandoMs + r.TempoInternoMs + r.TempoExternoMs + r.TempoPlanejadoMs + r.TempoSemComunicacaoMs);
        Assert.Equal(4 * 3_600_000d, r.TempoDisponivelMs); // total − sem comunicação − programada
        Assert.Equal(2 * 3_600_000d, r.TempoRodandoMs);
        Assert.Equal(50.0, r.Disponibilidade);              // 2 h ÷ 4 h
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

        sessao.Producoes = Leituras(inicio, 900, 100);

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
        sessao.Producoes = Leituras(inicio, 800, 0);

        var resultado = _sut.Calcular(sessao, 1000);

        // Disponibilidade=100, Performance=80, Qualidade=100 → OEE=80
        Assert.Equal(100.0, resultado.Disponibilidade);
        Assert.Equal(80.0, resultado.Performance);
        Assert.Equal(100.0, resultado.Qualidade);
        Assert.Equal(80.0, resultado.Oee);
    }

    // ── Paradas não classificadas (sem motivo) ────────────────────

    [Fact]
    public void ParadaSemMotivo_Finalizada_ContaComoInterna()
    {
        var inicio = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var fim = inicio.AddHours(2);
        var sessao = CriarSessao(inicio, fim);

        sessao.Paradas = [CriarParadaSemMotivo(inicio.AddHours(1), fim)];

        var resultado = _sut.Calcular(sessao, 1000);

        // Antes era ignorada e a Disponibilidade saía 100%.
        Assert.Equal(50.0, resultado.Disponibilidade);
        Assert.Equal(3_600_000, resultado.TempoInternoMs);
        Assert.Equal(1, resultado.NumParadasInternas);
    }

    [Fact]
    public void ParadaSemMotivo_NaoAfetaTempoExternoNemPlanejado()
    {
        var inicio = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var fim = inicio.AddHours(4);
        var sessao = CriarSessao(inicio, fim);

        sessao.Paradas = [
            CriarParadaSemMotivo(inicio, inicio.AddHours(1)),
            CriarParada(inicio.AddHours(1), inicio.AddHours(2), TipoParada.Externa),
            CriarParada(inicio.AddHours(2), inicio.AddHours(3), TipoParada.Planejada)
        ];

        var resultado = _sut.Calcular(sessao, 1000);

        // Total 4h, planejada 1h → disponível 3h; interna (sem motivo) 1h e externa 1h → rodando 1h.
        Assert.Equal(33.3, resultado.Disponibilidade);
        Assert.Equal(3_600_000, resultado.TempoExternoMs);
        Assert.Equal(3_600_000, resultado.TempoPlanejadoMs);
        Assert.Equal(1, resultado.NumParadasInternas);
        Assert.Equal(1, resultado.NumParadasExternas);
        Assert.Equal(1, resultado.NumParadasPlanejadas);
    }
}
