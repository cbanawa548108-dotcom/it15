using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRLFruitstandESS.Migrations
{
    /// <inheritdoc />
    public partial class AddSpoilageEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new columns to SpoilageRecords
            migrationBuilder.Sql("ALTER TABLE `SpoilageRecords` ADD COLUMN IF NOT EXISTS `SpoilageType` longtext CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Manual';");
            migrationBuilder.Sql("ALTER TABLE `SpoilageRecords` ADD COLUMN IF NOT EXISTS `IsSellable` tinyint(1) NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE `SpoilageRecords` ADD COLUMN IF NOT EXISTS `DiscountedPrice` decimal(18,2) NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE `SpoilageRecords` ADD COLUMN IF NOT EXISTS `IsSold` tinyint(1) NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE `SpoilageRecords` ADD COLUMN IF NOT EXISTS `SoldAt` datetime(6) NULL;");
            migrationBuilder.Sql("ALTER TABLE `SpoilageRecords` ADD COLUMN IF NOT EXISTS `SoldQuantity` int NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE `SpoilageRecords` ADD COLUMN IF NOT EXISTS `SoldRevenue` decimal(18,2) NOT NULL DEFAULT 0;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `SpoilageRecords` DROP COLUMN IF EXISTS `SpoilageType`;");
            migrationBuilder.Sql("ALTER TABLE `SpoilageRecords` DROP COLUMN IF EXISTS `IsSellable`;");
            migrationBuilder.Sql("ALTER TABLE `SpoilageRecords` DROP COLUMN IF EXISTS `DiscountedPrice`;");
            migrationBuilder.Sql("ALTER TABLE `SpoilageRecords` DROP COLUMN IF EXISTS `IsSold`;");
            migrationBuilder.Sql("ALTER TABLE `SpoilageRecords` DROP COLUMN IF EXISTS `SoldAt`;");
            migrationBuilder.Sql("ALTER TABLE `SpoilageRecords` DROP COLUMN IF EXISTS `SoldQuantity`;");
            migrationBuilder.Sql("ALTER TABLE `SpoilageRecords` DROP COLUMN IF EXISTS `SoldRevenue`;");
        }
    }
}
