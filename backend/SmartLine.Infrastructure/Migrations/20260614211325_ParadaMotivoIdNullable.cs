using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ParadaMotivoIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Paradas_MotivosParada_MotivoId",
                table: "Paradas");

            migrationBuilder.AlterColumn<Guid>(
                name: "MotivoId",
                table: "Paradas",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_Paradas_MotivosParada_MotivoId",
                table: "Paradas",
                column: "MotivoId",
                principalTable: "MotivosParada",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Paradas_MotivosParada_MotivoId",
                table: "Paradas");

            migrationBuilder.AlterColumn<Guid>(
                name: "MotivoId",
                table: "Paradas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Paradas_MotivosParada_MotivoId",
                table: "Paradas",
                column: "MotivoId",
                principalTable: "MotivosParada",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
