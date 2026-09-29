using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TextosEntradasWise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TextosEntradasWise",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MaquinaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Canal = table.Column<int>(type: "INTEGER", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    TextoAtivo = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    TextoNormal = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextosEntradasWise", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TextosEntradasWise_Maquinas_MaquinaId",
                        column: x => x.MaquinaId,
                        principalTable: "Maquinas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TextosEntradasWise_MaquinaId_Canal",
                table: "TextosEntradasWise",
                columns: new[] { "MaquinaId", "Canal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TextosEntradasWise");
        }
    }
}
