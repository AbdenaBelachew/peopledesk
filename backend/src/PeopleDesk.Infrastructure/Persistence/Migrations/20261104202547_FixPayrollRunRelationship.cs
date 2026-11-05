using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PeopleDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixPayrollRunRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayrollRuns_PayrollPeriods_PeriodId",
                table: "PayrollRuns");

            migrationBuilder.DropIndex(
                name: "IX_PayrollRuns_PeriodId",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "PeriodId",
                table: "PayrollRuns");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PeriodId",
                table: "PayrollRuns",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_PeriodId",
                table: "PayrollRuns",
                column: "PeriodId");

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollRuns_PayrollPeriods_PeriodId",
                table: "PayrollRuns",
                column: "PeriodId",
                principalTable: "PayrollPeriods",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
