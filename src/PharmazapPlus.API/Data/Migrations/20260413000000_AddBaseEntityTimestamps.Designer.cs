using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PharmazapPlus.API.Data;

#nullable disable

namespace PharmazapPlus.API.Migrations
{
    [DbContext(typeof(PharmazapDbContext))]
    [Migration("20260413000000_AddBaseEntityTimestamps")]
    partial class AddBaseEntityTimestamps
    {
        /// <inheritdoc />
        protected void BuildTargetModel(ModelBuilder modelBuilder)
        {
            // This will be auto-generated on next migration
            // For now, we can leave it empty or copy from the last migration's designer
        }
    }
}