using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Banking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStandingOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "standing_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    source_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    frequency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    next_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_run_error = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_standing_orders", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_standing_orders_owner",
                table: "standing_orders",
                column: "owner");

            migrationBuilder.CreateIndex(
                name: "ix_standing_orders_status_next_run_at",
                table: "standing_orders",
                columns: new[] { "status", "next_run_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "standing_orders");
        }
    }
}
