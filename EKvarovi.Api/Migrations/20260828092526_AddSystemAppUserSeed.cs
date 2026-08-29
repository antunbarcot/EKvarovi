using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EKvarovi.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemAppUserSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AppUsers",
                columns: new[] { "Id", "CreatedAt", "DisplayName", "Email", "EmployeeId", "IsActive", "PasswordHash" },
                values: new object[] { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sistem", "sistem@ekvarovi.local", null, false, "SISTEM-RACUN-BEZ-PRIJAVE" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AppUsers",
                keyColumn: "Id",
                keyValue: 1);
        }
    }
}
