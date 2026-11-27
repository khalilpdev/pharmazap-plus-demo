using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmazapPlus.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCanalMetaConfigSourceAndOwnCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetaAppId",
                table: "Canais",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaAppSecret",
                table: "Canais",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MetaConfigSource",
                table: "Canais",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MetaAppId",
                table: "Canais");

            migrationBuilder.DropColumn(
                name: "MetaAppSecret",
                table: "Canais");

            migrationBuilder.DropColumn(
                name: "MetaConfigSource",
                table: "Canais");
        }
    }
}
