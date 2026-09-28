using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DispositivoIotPorIp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DispositivosIot_IdentificadorMqtt",
                table: "DispositivosIot");

            migrationBuilder.DropColumn(
                name: "IdentificadorMqtt",
                table: "DispositivosIot");

            migrationBuilder.AlterColumn<string>(
                name: "EnderecoIp",
                table: "DispositivosIot",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DispositivosIot_EnderecoIp",
                table: "DispositivosIot",
                column: "EnderecoIp",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DispositivosIot_EnderecoIp",
                table: "DispositivosIot");

            migrationBuilder.AlterColumn<string>(
                name: "EnderecoIp",
                table: "DispositivosIot",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "IdentificadorMqtt",
                table: "DispositivosIot",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_DispositivosIot_IdentificadorMqtt",
                table: "DispositivosIot",
                column: "IdentificadorMqtt",
                unique: true);
        }
    }
}
