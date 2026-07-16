using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CamposDinamicos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CamposMaquina",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaquinaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Unidade = table.Column<string>(type: "text", nullable: true),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CamposMaquina", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CamposMaquina_Maquinas_MaquinaId",
                        column: x => x.MaquinaId,
                        principalTable: "Maquinas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeiturasExtra",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampoMaquinaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Hora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeiturasExtra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeiturasExtra_CamposMaquina_CampoMaquinaId",
                        column: x => x.CampoMaquinaId,
                        principalTable: "CamposMaquina",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeiturasExtra_Sessoes_SessaoId",
                        column: x => x.SessaoId,
                        principalTable: "Sessoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessoesCampo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampoMaquinaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessoesCampo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessoesCampo_CamposMaquina_CampoMaquinaId",
                        column: x => x.CampoMaquinaId,
                        principalTable: "CamposMaquina",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessoesCampo_Sessoes_SessaoId",
                        column: x => x.SessaoId,
                        principalTable: "Sessoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CamposMaquina_MaquinaId",
                table: "CamposMaquina",
                column: "MaquinaId");

            migrationBuilder.CreateIndex(
                name: "IX_LeiturasExtra_CampoMaquinaId",
                table: "LeiturasExtra",
                column: "CampoMaquinaId");

            migrationBuilder.CreateIndex(
                name: "IX_LeiturasExtra_SessaoId",
                table: "LeiturasExtra",
                column: "SessaoId");

            migrationBuilder.CreateIndex(
                name: "IX_SessoesCampo_CampoMaquinaId",
                table: "SessoesCampo",
                column: "CampoMaquinaId");

            migrationBuilder.CreateIndex(
                name: "IX_SessoesCampo_SessaoId",
                table: "SessoesCampo",
                column: "SessaoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeiturasExtra");

            migrationBuilder.DropTable(
                name: "SessoesCampo");

            migrationBuilder.DropTable(
                name: "CamposMaquina");
        }
    }
}
