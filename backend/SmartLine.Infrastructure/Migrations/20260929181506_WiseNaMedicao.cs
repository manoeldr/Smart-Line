using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WiseNaMedicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnderecoIpWise",
                table: "Acompanhamentos",
                type: "TEXT",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimaMensagemWiseEm",
                table: "Acompanhamentos",
                type: "TEXT",
                nullable: true);

            // O WISE deixa de ser cadastrado na máquina e passa a ser informado na medição.
            // As coletas em andamento herdam o IP e a última mensagem do WISE que estava
            // cadastrado (e ativo) na máquina delas; as finalizadas ficam sem (não havia registro).
            migrationBuilder.Sql("""
                UPDATE "Acompanhamentos"
                SET "EnderecoIpWise" = (
                        SELECT d."EnderecoIp" FROM "DispositivosIot" d
                        WHERE d."MaquinaLinhaId" = "Acompanhamentos"."MaquinaLinhaId" AND d."Ativo" = 1),
                    "UltimaMensagemWiseEm" = (
                        SELECT d."UltimaMensagemEm" FROM "DispositivosIot" d
                        WHERE d."MaquinaLinhaId" = "Acompanhamentos"."MaquinaLinhaId" AND d."Ativo" = 1)
                WHERE "FinalizadoEm" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Acompanhamentos_EnderecoIpWise_EmAndamento",
                table: "Acompanhamentos",
                column: "EnderecoIpWise",
                unique: true,
                filter: "\"FinalizadoEm\" IS NULL AND \"EnderecoIpWise\" IS NOT NULL");

            migrationBuilder.DropTable(
                name: "DispositivosIot");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Acompanhamentos_EnderecoIpWise_EmAndamento",
                table: "Acompanhamentos");

            migrationBuilder.DropColumn(
                name: "EnderecoIpWise",
                table: "Acompanhamentos");

            migrationBuilder.DropColumn(
                name: "UltimaMensagemWiseEm",
                table: "Acompanhamentos");

            migrationBuilder.CreateTable(
                name: "DispositivosIot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MaquinaLinhaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EnderecoIp = table.Column<string>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_DispositivosIot_EnderecoIp",
                table: "DispositivosIot",
                column: "EnderecoIp",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DispositivosIot_MaquinaLinhaId",
                table: "DispositivosIot",
                column: "MaquinaLinhaId",
                unique: true);
        }
    }
}
