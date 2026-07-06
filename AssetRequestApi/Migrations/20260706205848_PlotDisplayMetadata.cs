using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetRequestApi.Migrations
{
    /// <inheritdoc />
    public partial class PlotDisplayMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HatchColor",
                table: "Plots",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phase",
                table: "Plots",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HatchColor",
                table: "Plots");

            migrationBuilder.DropColumn(
                name: "Phase",
                table: "Plots");
        }
    }
}
