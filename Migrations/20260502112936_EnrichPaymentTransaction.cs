using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRLFruitstandESS.Migrations
{
    /// <inheritdoc />
    public partial class EnrichPaymentTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CardBrand",
                table: "PaymentTransactions",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CardLast4",
                table: "PaymentTransactions",
                type: "varchar(4)",
                maxLength: 4,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                table: "PaymentTransactions",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "FailureMessage",
                table: "PaymentTransactions",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsTestMode",
                table: "PaymentTransactions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PayMongoPaymentId",
                table: "PaymentTransactions",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethodType",
                table: "PaymentTransactions",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RawPayMongoResponse",
                table: "PaymentTransactions",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "PaymentTransactions",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CardBrand",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "CardLast4",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "FailureCode",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "FailureMessage",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "IsTestMode",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "PayMongoPaymentId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "PaymentMethodType",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "RawPayMongoResponse",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "PaymentTransactions");
        }
    }
}
