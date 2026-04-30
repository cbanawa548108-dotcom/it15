using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRLFruitstandESS.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Balance",
                table: "Suppliers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsPending",
                table: "SupplierPayments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SourceDeliveryId",
                table: "SupplierPayments",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Balance",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IsPending",
                table: "SupplierPayments");

            migrationBuilder.DropColumn(
                name: "SourceDeliveryId",
                table: "SupplierPayments");
        }
    }
}
