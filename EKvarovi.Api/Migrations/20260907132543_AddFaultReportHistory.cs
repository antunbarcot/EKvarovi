using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EKvarovi.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFaultReportHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FaultReportHistoryEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FaultReportId = table.Column<int>(type: "INTEGER", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ChangedByAppUserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaultReportHistoryEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaultReportHistoryEvents_AppUsers_ChangedByAppUserId",
                        column: x => x.ChangedByAppUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FaultReportHistoryEvents_FaultReports_FaultReportId",
                        column: x => x.FaultReportId,
                        principalTable: "FaultReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FaultReportHistoryEvents_ChangedByAppUserId",
                table: "FaultReportHistoryEvents",
                column: "ChangedByAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FaultReportHistoryEvents_FaultReportId",
                table: "FaultReportHistoryEvents",
                column: "FaultReportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FaultReportHistoryEvents");
        }
    }
}
