using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OEIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordResetToken",
                table: "Employees",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetTokenExpiresUtc",
                table: "Employees",
                type: "datetime2",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Employees",
                keyColumn: "EmployeeId",
                keyValue: 1000,
                columns: new[] { "CreatedAt", "PasswordResetToken", "PasswordResetTokenExpiresUtc" },
                values: new object[] { new DateTime(2026, 5, 19, 7, 6, 54, 791, DateTimeKind.Utc).AddTicks(675), null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordResetToken",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PasswordResetTokenExpiresUtc",
                table: "Employees");

            migrationBuilder.UpdateData(
                table: "Employees",
                keyColumn: "EmployeeId",
                keyValue: 1000,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 14, 4, 3, 48, 168, DateTimeKind.Utc).AddTicks(8970));
        }
    }
}
