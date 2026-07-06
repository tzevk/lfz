using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssetRequestApi.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeWorkflowStatusNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Plots SET Status = 'Free' WHERE Status = 'Available';");
            migrationBuilder.Sql("UPDATE Plots SET Status = 'Occupied' WHERE Status = 'Allocated';");
            migrationBuilder.Sql("UPDATE Plots SET Status = 'PendingReview' WHERE Status = 'UnderReview';");

            migrationBuilder.Sql("UPDATE PlotRequests SET Status = 'Pending' WHERE Status IN ('Submitted', 'UnderReview');");

            migrationBuilder.Sql("UPDATE PlotStatusHistory SET FromStatus = 'Free' WHERE FromStatus = 'Available';");
            migrationBuilder.Sql("UPDATE PlotStatusHistory SET ToStatus = 'Free' WHERE ToStatus = 'Available';");
            migrationBuilder.Sql("UPDATE PlotStatusHistory SET FromStatus = 'Occupied' WHERE FromStatus = 'Allocated';");
            migrationBuilder.Sql("UPDATE PlotStatusHistory SET ToStatus = 'Occupied' WHERE ToStatus = 'Allocated';");
            migrationBuilder.Sql("UPDATE PlotStatusHistory SET FromStatus = 'PendingReview' WHERE FromStatus = 'UnderReview';");
            migrationBuilder.Sql("UPDATE PlotStatusHistory SET ToStatus = 'PendingReview' WHERE ToStatus = 'UnderReview';");

            migrationBuilder.Sql("UPDATE AppSettings SET [Key] = 'Palette.Plot.Free', [Description] = 'Map colour for free plots.' WHERE [Key] = 'Palette.Plot.Available' AND NOT EXISTS (SELECT 1 FROM AppSettings WHERE [Key] = 'Palette.Plot.Free');");
            migrationBuilder.Sql("UPDATE AppSettings SET [Key] = 'Palette.Plot.Occupied', [Description] = 'Map colour for occupied plots.' WHERE [Key] = 'Palette.Plot.Allocated' AND NOT EXISTS (SELECT 1 FROM AppSettings WHERE [Key] = 'Palette.Plot.Occupied');");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM AppSettings WHERE [Key] = 'Palette.Plot.PendingReview') INSERT INTO AppSettings ([Key], [Value], [ValueType], [Description]) VALUES ('Palette.Plot.PendingReview', '#E879F9', 'Color', 'Map colour for pending review plots.');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Plots SET Status = 'Available' WHERE Status = 'Free';");
            migrationBuilder.Sql("UPDATE Plots SET Status = 'Allocated' WHERE Status = 'Occupied';");
            migrationBuilder.Sql("UPDATE Plots SET Status = 'UnderReview' WHERE Status = 'PendingReview';");

            migrationBuilder.Sql("UPDATE PlotRequests SET Status = 'Submitted' WHERE Status = 'Pending';");

            migrationBuilder.Sql("UPDATE PlotStatusHistory SET FromStatus = 'Available' WHERE FromStatus = 'Free';");
            migrationBuilder.Sql("UPDATE PlotStatusHistory SET ToStatus = 'Available' WHERE ToStatus = 'Free';");
            migrationBuilder.Sql("UPDATE PlotStatusHistory SET FromStatus = 'Allocated' WHERE FromStatus = 'Occupied';");
            migrationBuilder.Sql("UPDATE PlotStatusHistory SET ToStatus = 'Allocated' WHERE ToStatus = 'Occupied';");
            migrationBuilder.Sql("UPDATE PlotStatusHistory SET FromStatus = 'UnderReview' WHERE FromStatus = 'PendingReview';");
            migrationBuilder.Sql("UPDATE PlotStatusHistory SET ToStatus = 'UnderReview' WHERE ToStatus = 'PendingReview';");

            migrationBuilder.Sql("UPDATE AppSettings SET [Key] = 'Palette.Plot.Available', [Description] = 'Map colour for available plots.' WHERE [Key] = 'Palette.Plot.Free' AND NOT EXISTS (SELECT 1 FROM AppSettings WHERE [Key] = 'Palette.Plot.Available');");
            migrationBuilder.Sql("UPDATE AppSettings SET [Key] = 'Palette.Plot.Allocated', [Description] = 'Map colour for allocated plots.' WHERE [Key] = 'Palette.Plot.Occupied' AND NOT EXISTS (SELECT 1 FROM AppSettings WHERE [Key] = 'Palette.Plot.Allocated');");
            migrationBuilder.Sql("DELETE FROM AppSettings WHERE [Key] = 'Palette.Plot.PendingReview';");
        }
    }
}