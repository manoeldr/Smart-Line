using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaColetaAutomatica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AcompanhamentoId",
                table: "Sessoes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MotivoFechamento",
                table: "Sessoes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RegraClassificacaoId",
                table: "Paradas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TempoDeteccaoParadaSegundos",
                table: "MaquinasLinha",
                type: "INTEGER",
                nullable: false,
                // Ajustado à mão (o gerado era 0): máquinas já cadastradas recebem o mesmo
                // padrão da entidade. Com 0, a coleta recusaria iniciar (Z precisa ser positivo).
                defaultValue: 60);

            migrationBuilder.CreateTable(
                name: "Acompanhamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MaquinaLinhaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IniciadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinalizadoEm = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FinalizadoPorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TempoDeteccaoParadaSegundos = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acompanhamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acompanhamentos_MaquinasLinha_MaquinaLinhaId",
                        column: x => x.MaquinaLinhaId,
                        principalTable: "MaquinasLinha",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Acompanhamentos_Usuarios_FinalizadoPorId",
                        column: x => x.FinalizadoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Acompanhamentos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConjuntosRegras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MaquinaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MaquinaLinhaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConjuntosRegras", x => x.Id);
                    table.CheckConstraint("CK_ConjuntoRegras_UmVinculo", "(\"MaquinaId\" IS NULL) <> (\"MaquinaLinhaId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_ConjuntosRegras_MaquinasLinha_MaquinaLinhaId",
                        column: x => x.MaquinaLinhaId,
                        principalTable: "MaquinasLinha",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConjuntosRegras_Maquinas_MaquinaId",
                        column: x => x.MaquinaId,
                        principalTable: "Maquinas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DispositivosIot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MaquinaLinhaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    IdentificadorMqtt = table.Column<string>(type: "TEXT", nullable: false),
                    EnderecoIp = table.Column<string>(type: "TEXT", nullable: true),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UltimaMensagemEm = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispositivosIot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DispositivosIot_MaquinasLinha_MaquinaLinhaId",
                        column: x => x.MaquinaLinhaId,
                        principalTable: "MaquinasLinha",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HistoricosClassificacaoParada",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParadaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MotivoAnteriorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MotivoNovoId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UsuarioId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AlteradoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricosClassificacaoParada", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricosClassificacaoParada_MotivosParada_MotivoAnteriorId",
                        column: x => x.MotivoAnteriorId,
                        principalTable: "MotivosParada",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricosClassificacaoParada_MotivosParada_MotivoNovoId",
                        column: x => x.MotivoNovoId,
                        principalTable: "MotivosParada",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricosClassificacaoParada_Paradas_ParadaId",
                        column: x => x.ParadaId,
                        principalTable: "Paradas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistoricosClassificacaoParada_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PeriodosSemComunicacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MaquinaLinhaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Inicio = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Fim = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodosSemComunicacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeriodosSemComunicacao_MaquinasLinha_MaquinaLinhaId",
                        column: x => x.MaquinaLinhaId,
                        principalTable: "MaquinasLinha",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AcompanhamentoCanais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AcompanhamentoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Canal = table.Column<int>(type: "INTEGER", nullable: false),
                    Multiplicador = table.Column<int>(type: "INTEGER", nullable: false),
                    UltimoValorBruto = table.Column<long>(type: "INTEGER", nullable: true),
                    UltimoValorEm = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcompanhamentoCanais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcompanhamentoCanais_Acompanhamentos_AcompanhamentoId",
                        column: x => x.AcompanhamentoId,
                        principalTable: "Acompanhamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegrasClassificacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConjuntoRegrasId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Prioridade = table.Column<int>(type: "INTEGER", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    MotivoParadaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ativa = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegrasClassificacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegrasClassificacao_ConjuntosRegras_ConjuntoRegrasId",
                        column: x => x.ConjuntoRegrasId,
                        principalTable: "ConjuntosRegras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegrasClassificacao_MotivosParada_MotivoParadaId",
                        column: x => x.MotivoParadaId,
                        principalTable: "MotivosParada",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CondicoesRegra",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RegraClassificacaoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Canal = table.Column<int>(type: "INTEGER", nullable: true),
                    TempoMinimoSegundos = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondicoesRegra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CondicoesRegra_RegrasClassificacao_RegraClassificacaoId",
                        column: x => x.RegraClassificacaoId,
                        principalTable: "RegrasClassificacao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sessoes_AcompanhamentoId",
                table: "Sessoes",
                column: "AcompanhamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Paradas_RegraClassificacaoId",
                table: "Paradas",
                column: "RegraClassificacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_AcompanhamentoCanais_AcompanhamentoId_Canal",
                table: "AcompanhamentoCanais",
                columns: new[] { "AcompanhamentoId", "Canal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Acompanhamentos_FinalizadoPorId",
                table: "Acompanhamentos",
                column: "FinalizadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Acompanhamentos_MaquinaLinhaId_EmAndamento",
                table: "Acompanhamentos",
                column: "MaquinaLinhaId",
                unique: true,
                filter: "\"FinalizadoEm\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Acompanhamentos_UsuarioId",
                table: "Acompanhamentos",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_CondicoesRegra_RegraClassificacaoId",
                table: "CondicoesRegra",
                column: "RegraClassificacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ConjuntosRegras_MaquinaId",
                table: "ConjuntosRegras",
                column: "MaquinaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConjuntosRegras_MaquinaLinhaId",
                table: "ConjuntosRegras",
                column: "MaquinaLinhaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DispositivosIot_IdentificadorMqtt",
                table: "DispositivosIot",
                column: "IdentificadorMqtt",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DispositivosIot_MaquinaLinhaId",
                table: "DispositivosIot",
                column: "MaquinaLinhaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricosClassificacaoParada_MotivoAnteriorId",
                table: "HistoricosClassificacaoParada",
                column: "MotivoAnteriorId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricosClassificacaoParada_MotivoNovoId",
                table: "HistoricosClassificacaoParada",
                column: "MotivoNovoId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricosClassificacaoParada_ParadaId_AlteradoEm",
                table: "HistoricosClassificacaoParada",
                columns: new[] { "ParadaId", "AlteradoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricosClassificacaoParada_UsuarioId",
                table: "HistoricosClassificacaoParada",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodosSemComunicacao_MaquinaLinhaId_Inicio",
                table: "PeriodosSemComunicacao",
                columns: new[] { "MaquinaLinhaId", "Inicio" });

            migrationBuilder.CreateIndex(
                name: "IX_RegrasClassificacao_ConjuntoRegrasId_Prioridade",
                table: "RegrasClassificacao",
                columns: new[] { "ConjuntoRegrasId", "Prioridade" });

            migrationBuilder.CreateIndex(
                name: "IX_RegrasClassificacao_MotivoParadaId",
                table: "RegrasClassificacao",
                column: "MotivoParadaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Paradas_RegrasClassificacao_RegraClassificacaoId",
                table: "Paradas",
                column: "RegraClassificacaoId",
                principalTable: "RegrasClassificacao",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Sessoes_Acompanhamentos_AcompanhamentoId",
                table: "Sessoes",
                column: "AcompanhamentoId",
                principalTable: "Acompanhamentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Paradas_RegrasClassificacao_RegraClassificacaoId",
                table: "Paradas");

            migrationBuilder.DropForeignKey(
                name: "FK_Sessoes_Acompanhamentos_AcompanhamentoId",
                table: "Sessoes");

            migrationBuilder.DropTable(
                name: "AcompanhamentoCanais");

            migrationBuilder.DropTable(
                name: "CondicoesRegra");

            migrationBuilder.DropTable(
                name: "DispositivosIot");

            migrationBuilder.DropTable(
                name: "HistoricosClassificacaoParada");

            migrationBuilder.DropTable(
                name: "PeriodosSemComunicacao");

            migrationBuilder.DropTable(
                name: "Acompanhamentos");

            migrationBuilder.DropTable(
                name: "RegrasClassificacao");

            migrationBuilder.DropTable(
                name: "ConjuntosRegras");

            migrationBuilder.DropIndex(
                name: "IX_Sessoes_AcompanhamentoId",
                table: "Sessoes");

            migrationBuilder.DropIndex(
                name: "IX_Paradas_RegraClassificacaoId",
                table: "Paradas");

            migrationBuilder.DropColumn(
                name: "AcompanhamentoId",
                table: "Sessoes");

            migrationBuilder.DropColumn(
                name: "MotivoFechamento",
                table: "Sessoes");

            migrationBuilder.DropColumn(
                name: "RegraClassificacaoId",
                table: "Paradas");

            migrationBuilder.DropColumn(
                name: "TempoDeteccaoParadaSegundos",
                table: "MaquinasLinha");
        }
    }
}
