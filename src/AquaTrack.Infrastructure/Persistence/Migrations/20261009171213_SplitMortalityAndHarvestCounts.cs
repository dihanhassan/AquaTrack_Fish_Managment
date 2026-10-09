using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AquaTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitMortalityAndHarvestCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HarvestedCount",
                table: "FishStocks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MortalityCount",
                table: "FishStocks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HarvestedCount",
                table: "FishStocks");

            migrationBuilder.DropColumn(
                name: "MortalityCount",
                table: "FishStocks");
        }
    }
}
