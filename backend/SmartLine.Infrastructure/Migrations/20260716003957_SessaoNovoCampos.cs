using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SessaoNovoCampos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PrevisaoTermino",
                table: "Sessoes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SobreVelocidade",
                table: "Sessoes",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TipoColeta",
                table: "Sessoes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "VelocidadeNominal",
                table: "Sessoes",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrevisaoTermino",
                table: "Sessoes");

            migrationBuilder.DropColumn(
                name: "SobreVelocidade",
                table: "Sessoes");

            migrationBuilder.DropColumn(
                name: "TipoColeta",
                table: "Sessoes");

            migrationBuilder.DropColumn(
                name: "VelocidadeNominal",
                table: "Sessoes");
        }
    }
}
