using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmazapPlus.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFarmaciaLogoUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "Farmacias",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "Farmacias");
        }
    }
}
