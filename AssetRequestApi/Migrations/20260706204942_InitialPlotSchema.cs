using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace AssetRequestApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialPlotSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ValueType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RoleFlag = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Requests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AllocatedToUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LegalName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Contact = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Industry = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Plots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LandUseType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AreaHectares = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CurrentTenantId = table.Column<int>(type: "int", nullable: true),
                    Boundary = table.Column<Geometry>(type: "geometry", nullable: true),
                    SvgPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Centroid = table.Column<Point>(type: "geometry", nullable: true),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    MultiTenantBlockEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Plots_Tenants_CurrentTenantId",
                        column: x => x.CurrentTenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PlotRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlotId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RequestType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IntendedUse = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestedStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DecisionByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DecisionAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlotRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlotRequests_AspNetUsers_DecisionByUserId",
                        column: x => x.DecisionByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlotRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlotRequests_Plots_PlotId",
                        column: x => x.PlotId,
                        principalTable: "Plots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlotRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlotTenantBlocks",
                columns: table => new
                {
                    PlotId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    BlockedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    BlockedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlotTenantBlocks", x => new { x.PlotId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_PlotTenantBlocks_AspNetUsers_BlockedByUserId",
                        column: x => x.BlockedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlotTenantBlocks_Plots_PlotId",
                        column: x => x.PlotId,
                        principalTable: "Plots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlotTenantBlocks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlotStatusHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlotId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ToStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: true),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OriginatingRequestId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlotStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlotStatusHistory_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlotStatusHistory_PlotRequests_OriginatingRequestId",
                        column: x => x.OriginatingRequestId,
                        principalTable: "PlotRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlotStatusHistory_Plots_PlotId",
                        column: x => x.PlotId,
                        principalTable: "Plots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlotStatusHistory_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppSettings_Key",
                table: "AppSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlotRequests_DecisionByUserId",
                table: "PlotRequests",
                column: "DecisionByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlotRequests_PlotId",
                table: "PlotRequests",
                column: "PlotId");

            migrationBuilder.CreateIndex(
                name: "IX_PlotRequests_RequestedByUserId",
                table: "PlotRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlotRequests_TenantId",
                table: "PlotRequests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Plots_Code",
                table: "Plots",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plots_CurrentTenantId",
                table: "Plots",
                column: "CurrentTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PlotStatusHistory_ActorUserId",
                table: "PlotStatusHistory",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlotStatusHistory_OriginatingRequestId",
                table: "PlotStatusHistory",
                column: "OriginatingRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PlotStatusHistory_PlotId",
                table: "PlotStatusHistory",
                column: "PlotId");

            migrationBuilder.CreateIndex(
                name: "IX_PlotStatusHistory_TenantId",
                table: "PlotStatusHistory",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PlotTenantBlocks_BlockedByUserId",
                table: "PlotTenantBlocks",
                column: "BlockedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlotTenantBlocks_TenantId",
                table: "PlotTenantBlocks",
                column: "TenantId");

            migrationBuilder.InsertData(
                table: "AppSettings",
                columns: new[] { "Key", "Value", "ValueType", "Description" },
                values: new object[,]
                {
                    { "Feature.AllowMultiTenantBlock", "false", "Boolean", "Global gate for the multi-tenant plot block exception." },
                    { "Palette.Plot.Available", "#5BBF72", "Color", "Map colour for available plots." },
                    { "Palette.Plot.Blocked", "#F0B84D", "Color", "Map colour for blocked plots." },
                    { "Palette.Plot.Allocated", "#3B82C4", "Color", "Map colour for allocated plots." },
                    { "Palette.Plot.Unavailable", "#9CA3AF", "Color", "Map colour for unavailable plots." }
                });

            SeedPlotsFromJson(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "PlotStatusHistory");

            migrationBuilder.DropTable(
                name: "PlotTenantBlocks");

            migrationBuilder.DropTable(
                name: "Requests");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "PlotRequests");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Plots");

            migrationBuilder.DropTable(
                name: "Tenants");
        }

        private static void SeedPlotsFromJson(MigrationBuilder migrationBuilder)
        {
            var seedPath = ResolveSeedPath();
            if (seedPath is null)
            {
                return;
            }

            var seedItems = JsonSerializer.Deserialize<List<PlotSeedItem>>(File.ReadAllText(seedPath), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<PlotSeedItem>();

            foreach (var item in seedItems.Where(item => !string.IsNullOrWhiteSpace(item.Code)))
            {
                migrationBuilder.InsertData(
                    table: "Plots",
                    columns: new[]
                    {
                        "Code",
                        "DisplayName",
                        "LandUseType",
                        "AreaHectares",
                        "Status",
                        "SvgPath",
                        "Centroid",
                        "IsLocked",
                        "MultiTenantBlockEnabled"
                    },
                    values: new object[]
                    {
                        item.Code.Trim(),
                        string.IsNullOrWhiteSpace(item.DisplayName) ? item.Code.Trim() : item.DisplayName.Trim(),
                        string.IsNullOrWhiteSpace(item.LandUseType) ? "Unspecified" : item.LandUseType.Trim(),
                        item.AreaHectares,
                        string.IsNullOrWhiteSpace(item.Status) ? "Available" : item.Status.Trim(),
                        item.SvgPath,
                        item.Centroid is null ? null : new Point(item.Centroid.X, item.Centroid.Y),
                        item.IsLocked,
                        item.MultiTenantBlockEnabled
                    });
            }
        }

        private static string ResolveSeedPath()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Seed", "plots-seed.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "Seed", "plots-seed.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "AssetRequestApi", "Seed", "plots-seed.json")
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private sealed class PlotSeedItem
        {
            public string Code { get; set; } = string.Empty;
            public string DisplayName { get; set; }
            public string LandUseType { get; set; }
            public decimal AreaHectares { get; set; }
            public string Status { get; set; }
            public string SvgPath { get; set; }
            public PlotSeedCentroid Centroid { get; set; }
            public bool IsLocked { get; set; }
            public bool MultiTenantBlockEnabled { get; set; }
        }

        private sealed class PlotSeedCentroid
        {
            public double X { get; set; }
            public double Y { get; set; }
        }
    }
}
