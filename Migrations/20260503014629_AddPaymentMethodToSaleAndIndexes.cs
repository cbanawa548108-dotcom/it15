using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRLFruitstandESS.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentMethodToSaleAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use IF EXISTS to handle cases where the previous migration's indexes
            // were never created (e.g. AddLoginAttempts was skipped or partially applied).
            migrationBuilder.Sql(
                "ALTER TABLE `LoginAttempts` DROP INDEX IF EXISTS `IX_LoginAttempts_AttemptedAt`;");

            migrationBuilder.Sql(
                "ALTER TABLE `LoginAttempts` DROP INDEX IF EXISTS `IX_LoginAttempts_IpAddress_AttemptedAt`;");

            // Use IF NOT EXISTS to handle cases where the column was already added manually.
            migrationBuilder.Sql(@"
                ALTER TABLE `Sales`
                ADD COLUMN IF NOT EXISTS `PaymentMethod` varchar(50) CHARACTER SET utf8mb4 NULL;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE `PaymentTransactions`
                ADD COLUMN IF NOT EXISTS `PendingSaleDataJson` longtext CHARACTER SET utf8mb4 NULL;
            ");

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                table: "LoginAttempts",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(256)",
                oldMaxLength: 256)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "UserAgent",
                table: "LoginAttempts",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(250)",
                oldMaxLength: 250)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "LoginAttempts",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(45)",
                oldMaxLength: 45)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "FailReason",
                table: "LoginAttempts",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `SpoilageRecords` (
                    `Id`            INT NOT NULL AUTO_INCREMENT,
                    `ProductId`     INT NOT NULL,
                    `Quantity`      INT NOT NULL DEFAULT 0,
                    `EstimatedLoss` DECIMAL(18,2) NOT NULL DEFAULT 0,
                    `Reason`        LONGTEXT CHARACTER SET utf8mb4 NOT NULL,
                    `RecordedBy`    LONGTEXT CHARACTER SET utf8mb4 NOT NULL,
                    `RecordedAt`    DATETIME(6) NOT NULL DEFAULT '0001-01-01 00:00:00.000000',
                    `Notes`         LONGTEXT CHARACTER SET utf8mb4 NULL,
                    PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_SpoilageRecords_Products_ProductId`
                        FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE CASCADE
                ) CHARACTER SET utf8mb4;
            ");

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS `IX_SpoilageRecords_ProductId` ON `SpoilageRecords` (`ProductId`);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpoilageRecords");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "PendingSaleDataJson",
                table: "PaymentTransactions");

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                table: "LoginAttempts",
                type: "varchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "UserAgent",
                table: "LoginAttempts",
                type: "varchar(250)",
                maxLength: 250,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "LoginAttempts",
                type: "varchar(45)",
                maxLength: 45,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "FailReason",
                table: "LoginAttempts",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttempts_AttemptedAt",
                table: "LoginAttempts",
                column: "AttemptedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttempts_IpAddress_AttemptedAt",
                table: "LoginAttempts",
                columns: new[] { "IpAddress", "AttemptedAt" });
        }
    }
}
