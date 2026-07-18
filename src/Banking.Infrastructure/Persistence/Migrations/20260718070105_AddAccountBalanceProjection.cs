using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Banking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountBalanceProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_balances",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    debits = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    credits = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_balances", x => x.account_id);
                });

            // The projection is derived data: seed it from the ledger so accounts
            // that moved money before this table existed read correctly.
            migrationBuilder.Sql("""
                INSERT INTO account_balances (account_id, debits, credits, updated_at)
                SELECT account_id,
                       COALESCE(SUM(amount) FILTER (WHERE direction = 'Debit'), 0),
                       COALESCE(SUM(amount) FILTER (WHERE direction = 'Credit'), 0),
                       now()
                FROM ledger_entries
                GROUP BY account_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_balances");
        }
    }
}
