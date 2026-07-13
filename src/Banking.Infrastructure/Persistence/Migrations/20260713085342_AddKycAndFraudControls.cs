using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Banking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKycAndFraudControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Defaults backfill rows created before this migration: they get the
            // domain's default limit and start unverified like any new account.
            migrationBuilder.AddColumn<decimal>(
                name: "daily_transfer_limit",
                table: "accounts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 20_000m);

            migrationBuilder.AddColumn<string>(
                name: "kyc_status",
                table: "accounts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.CreateTable(
                name: "fraud_alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    flagged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fraud_alerts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fraud_alerts_transaction_id_rule",
                table: "fraud_alerts",
                columns: new[] { "transaction_id", "rule" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fraud_alerts");

            migrationBuilder.DropColumn(
                name: "daily_transfer_limit",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "kyc_status",
                table: "accounts");
        }
    }
}
