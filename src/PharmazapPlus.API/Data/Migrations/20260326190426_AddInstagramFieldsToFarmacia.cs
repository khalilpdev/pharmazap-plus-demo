using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmazapPlus.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInstagramFieldsToFarmacia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InstagramAccessToken",
                table: "Farmacias",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstagramAccountId",
                table: "Farmacias",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PageId",
                table: "Farmacias",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InstagramAccessToken",
                table: "Farmacias");

            migrationBuilder.DropColumn(
                name: "InstagramAccountId",
                table: "Farmacias");

            migrationBuilder.DropColumn(
                name: "PageId",
                table: "Farmacias");
        }
    }
}
