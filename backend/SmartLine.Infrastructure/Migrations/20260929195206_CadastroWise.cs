using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CadastroWise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Wises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnderecoIp = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wises", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Wises_EnderecoIp",
                table: "Wises",
                column: "EnderecoIp",
                unique: true);

            // Os WISE já usados em medições entram no cadastro (a medição em andamento continua
            // válida e eles aparecem na lista). Data do cadastro = primeira medição com o IP.
            // Guid no formato que o EF grava no SQLite (texto maiúsculo com hífens).
            migrationBuilder.Sql("""
                INSERT INTO "Wises" ("Id", "EnderecoIp", "Nome", "CriadoEm")
                SELECT upper(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' ||
                             hex(randomblob(2)) || '-' || hex(randomblob(6))),
                       "EnderecoIpWise", NULL, MIN("IniciadoEm")
                FROM "Acompanhamentos"
                WHERE "EnderecoIpWise" IS NOT NULL
                GROUP BY "EnderecoIpWise";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Wises");
        }
    }
}
