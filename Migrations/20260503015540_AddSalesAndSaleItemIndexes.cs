using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRLFruitstandESS.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesAndSaleItemIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use IF NOT EXISTS / conditional SQL to avoid duplicate-column errors
            // on databases where this migration was partially applied.
            migrationBuilder.Sql(@"
                ALTER TABLE `Sales`
                MODIFY COLUMN IF EXISTS `Status`
                    varchar(255) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Completed';
            ");

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS `IX_Sales_SaleDate` ON `Sales` (`SaleDate`);");

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS `IX_Sales_Status` ON `Sales` (`Status`(255));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_SaleDate",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_Status",
                table: "Sales");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Sales",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
