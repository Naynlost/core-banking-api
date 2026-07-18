using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Banking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFraudAlertReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "resolution_note",
                table: "fraud_alerts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "resolved_at",
                table: "fraud_alerts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "fraud_alerts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Open");

            migrationBuilder.CreateIndex(
                name: "ix_fraud_alerts_status",
                table: "fraud_alerts",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_fraud_alerts_status",
                table: "fraud_alerts");

            migrationBuilder.DropColumn(
                name: "resolution_note",
                table: "fraud_alerts");

            migrationBuilder.DropColumn(
                name: "resolved_at",
                table: "fraud_alerts");

            migrationBuilder.DropColumn(
                name: "status",
                table: "fraud_alerts");
        }
    }
}
