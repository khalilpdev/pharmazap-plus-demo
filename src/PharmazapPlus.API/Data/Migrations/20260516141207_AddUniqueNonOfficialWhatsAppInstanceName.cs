using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmazapPlus.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueNonOfficialWhatsAppInstanceName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Canais_NonOfficialWhatsApp_InstanceName"
                ON "Canais" ("FarmaciaIntegracosId", lower(btrim("InstanceName")))
                WHERE "Tipo" = 1
                  AND NOT "IsOfficial"
                  AND "InstanceName" IS NOT NULL
                  AND btrim("InstanceName") <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_Canais_NonOfficialWhatsApp_InstanceName";
                """);
        }
    }
}
