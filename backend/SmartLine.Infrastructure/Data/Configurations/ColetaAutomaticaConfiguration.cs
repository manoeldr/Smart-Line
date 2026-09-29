using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartLine.Core.Entities.Global;
using SmartLine.Core.Entities.Tenant;
using SmartLine.Core.Interfaces;
using SmartLine.Core.Iot;

namespace SmartLine.Infrastructure.Data.Configurations;

// Mapeamento das entidades da coleta automática (Semi Automático e, depois, Automático).
//
// Regras de exclusão adotadas:
// - Filho estrutural some com o pai (Cascade): condição com a regra, regra com o conjunto,
//   canal com o acompanhamento, histórico com a parada, e tudo que pende da máquina da linha
//   (mesmo comportamento que Sessao já tem por convenção).
// - Referência a dado de auditoria é protegida (Restrict): motivo de parada e usuário. O
//   sistema só desativa esses registros (Ativo = false), nunca apaga, então a trava não
//   atrapalha nenhuma tela e impede perder de quem/qual motivo uma classificação veio.
// - Referência opcional que pode sumir sem invalidar o registro fica nula (SetNull): regra
//   que classificou a parada, acompanhamento da sessão.

public class ConjuntoRegrasConfiguration : IEntityTypeConfiguration<ConjuntoRegras>
{
    public void Configure(EntityTypeBuilder<ConjuntoRegras> b)
    {
        // Exatamente um vínculo: padrão do catálogo OU customizado da máquina da linha.
        b.ToTable(t => t.HasCheckConstraint(
            "CK_ConjuntoRegras_UmVinculo",
            "(\"MaquinaId\" IS NULL) <> (\"MaquinaLinhaId\" IS NULL)"));

        b.HasOne(c => c.Maquina).WithMany()
            .HasForeignKey(c => c.MaquinaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(c => c.MaquinaLinha).WithMany()
            .HasForeignKey(c => c.MaquinaLinhaId)
            .OnDelete(DeleteBehavior.Cascade);

        // No máximo um conjunto por máquina do catálogo e um por máquina da linha.
        // (Índice único no SQLite aceita vários NULL, então os dois convivem.)
        b.HasIndex(c => c.MaquinaId).IsUnique();
        b.HasIndex(c => c.MaquinaLinhaId).IsUnique();
    }
}

public class RegraClassificacaoConfiguration : IEntityTypeConfiguration<RegraClassificacao>
{
    public void Configure(EntityTypeBuilder<RegraClassificacao> b)
    {
        b.HasOne(r => r.ConjuntoRegras).WithMany(c => c.Regras)
            .HasForeignKey(r => r.ConjuntoRegrasId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(r => r.MotivoParada).WithMany()
            .HasForeignKey(r => r.MotivoParadaId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(r => new { r.ConjuntoRegrasId, r.Prioridade });
    }
}

public class CondicaoRegraConfiguration : IEntityTypeConfiguration<CondicaoRegra>
{
    public void Configure(EntityTypeBuilder<CondicaoRegra> b)
    {
        b.HasOne(c => c.RegraClassificacao).WithMany(r => r.Condicoes)
            .HasForeignKey(c => c.RegraClassificacaoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AcompanhamentoConfiguration : IEntityTypeConfiguration<Acompanhamento>
{
    public void Configure(EntityTypeBuilder<Acompanhamento> b)
    {
        b.Ignore(a => a.EmAndamento);

        b.HasOne(a => a.MaquinaLinha).WithMany()
            .HasForeignKey(a => a.MaquinaLinhaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(a => a.Usuario).WithMany()
            .HasForeignKey(a => a.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(a => a.FinalizadoPor).WithMany()
            .HasForeignKey(a => a.FinalizadoPorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Garantia no banco: no máximo UM acompanhamento em andamento por máquina da linha.
        // Protege contra dois cliques em "Iniciar" ou duas estações iniciando juntas.
        b.HasIndex(a => a.MaquinaLinhaId)
            .IsUnique()
            .HasFilter("\"FinalizadoEm\" IS NULL")
            .HasDatabaseName("IX_Acompanhamentos_MaquinaLinhaId_EmAndamento");

        // Um WISE mede uma máquina por vez: o mesmo IP não pode estar em duas coletas em andamento.
        // Depois de finalizada, a coleta guarda o IP só como histórico e o WISE fica livre.
        b.Property(a => a.EnderecoIpWise).HasMaxLength(45);
        b.HasIndex(a => a.EnderecoIpWise)
            .IsUnique()
            .HasFilter("\"FinalizadoEm\" IS NULL AND \"EnderecoIpWise\" IS NOT NULL")
            .HasDatabaseName("IX_Acompanhamentos_EnderecoIpWise_EmAndamento");
    }
}

public class AcompanhamentoCanalConfiguration : IEntityTypeConfiguration<AcompanhamentoCanal>
{
    public void Configure(EntityTypeBuilder<AcompanhamentoCanal> b)
    {
        b.HasOne(c => c.Acompanhamento).WithMany(a => a.Canais)
            .HasForeignKey(c => c.AcompanhamentoId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(c => new { c.AcompanhamentoId, c.Canal }).IsUnique();
    }
}

public class HistoricoClassificacaoParadaConfiguration : IEntityTypeConfiguration<HistoricoClassificacaoParada>
{
    public void Configure(EntityTypeBuilder<HistoricoClassificacaoParada> b)
    {
        b.HasOne(h => h.Parada).WithMany(p => p.HistoricoClassificacao)
            .HasForeignKey(h => h.ParadaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(h => h.MotivoAnterior).WithMany()
            .HasForeignKey(h => h.MotivoAnteriorId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(h => h.MotivoNovo).WithMany()
            .HasForeignKey(h => h.MotivoNovoId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(h => h.Usuario).WithMany()
            .HasForeignKey(h => h.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(h => new { h.ParadaId, h.AlteradoEm });
    }
}

public class PeriodoSemComunicacaoConfiguration : IEntityTypeConfiguration<PeriodoSemComunicacao>
{
    public void Configure(EntityTypeBuilder<PeriodoSemComunicacao> b)
    {
        b.HasOne(p => p.MaquinaLinha).WithMany()
            .HasForeignKey(p => p.MaquinaLinhaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(p => new { p.MaquinaLinhaId, p.Inicio });
    }
}

// Ligações novas em entidades que já existiam (o resto delas continua por convenção).

public class SessaoColetaConfiguration : IEntityTypeConfiguration<Sessao>
{
    public void Configure(EntityTypeBuilder<Sessao> b)
    {
        b.HasOne(s => s.Acompanhamento).WithMany(a => a.Sessoes)
            .HasForeignKey(s => s.AcompanhamentoId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class ParadaColetaConfiguration : IEntityTypeConfiguration<Parada>
{
    public void Configure(EntityTypeBuilder<Parada> b)
    {
        b.HasOne(p => p.RegraClassificacao).WithMany()
            .HasForeignKey(p => p.RegraClassificacaoId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class TextoEntradaWiseConfiguration : IEntityTypeConfiguration<TextoEntradaWise>
{
    public void Configure(EntityTypeBuilder<TextoEntradaWise> b)
    {
        b.HasOne(t => t.Maquina).WithMany()
            .HasForeignKey(t => t.MaquinaId)
            .OnDelete(DeleteBehavior.Cascade);

        // No máximo um texto por entrada em cada máquina do catálogo.
        b.HasIndex(t => new { t.MaquinaId, t.Canal }).IsUnique();

        b.Property(t => t.Nome).HasMaxLength(TextosEntradasWise.TamanhoMaximo).IsRequired();
        b.Property(t => t.TextoAtivo).HasMaxLength(TextosEntradasWise.TamanhoMaximo);
        b.Property(t => t.TextoNormal).HasMaxLength(TextosEntradasWise.TamanhoMaximo);
    }
}

public class WiseConfiguration : IEntityTypeConfiguration<Wise>
{
    public void Configure(EntityTypeBuilder<Wise> b)
    {
        // É pelo IP que a mensagem MQTT encontra a medição: não pode repetir no cadastro.
        b.Property(w => w.EnderecoIp).HasMaxLength(45).IsRequired();
        b.HasIndex(w => w.EnderecoIp).IsUnique();
        b.Property(w => w.Nome).HasMaxLength(LimitesWise.TamanhoMaximoNome);
    }
}
