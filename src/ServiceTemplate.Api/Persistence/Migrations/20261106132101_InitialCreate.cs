using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceTemplate.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "service");

            migrationBuilder.CreateTable(
                name: "orders",
                schema: "service",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                    table.CheckConstraint("ck_orders_status", "status IN ('Draft', 'Submitted', 'Cancelled')");
                });

            migrationBuilder.CreateTable(
                name: "order_lines",
                schema: "service",
                columns: table => new
                {
                    sku = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_lines", x => new { x.order_id, x.sku });
                    table.CheckConstraint("ck_order_lines_quantity", "quantity BETWEEN 1 AND 1000");
                    table.CheckConstraint("ck_order_lines_unit_price", "unit_price > 0");
                    table.ForeignKey(
                        name: "FK_order_lines_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "service",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orders_customer_reference",
                schema: "service",
                table: "orders",
                column: "customer_reference");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_lines",
                schema: "service");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "service");
        }
    }
}
