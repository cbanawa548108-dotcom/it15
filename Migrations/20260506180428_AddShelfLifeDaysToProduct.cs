using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRLFruitstandESS.Migrations
{
    /// <inheritdoc />
    public partial class AddShelfLifeDaysToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountedPrice",
                table: "SpoilageRecords",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsSellable",
                table: "SpoilageRecords",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSold",
                table: "SpoilageRecords",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SoldAt",
                table: "SpoilageRecords",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SoldQuantity",
                table: "SpoilageRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "SoldRevenue",
                table: "SpoilageRecords",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SpoilageType",
                table: "SpoilageRecords",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountedPrice",
                table: "SpoilageRecords");

            migrationBuilder.DropColumn(
                name: "IsSellable",
                table: "SpoilageRecords");

            migrationBuilder.DropColumn(
                name: "IsSold",
                table: "SpoilageRecords");

            migrationBuilder.DropColumn(
                name: "SoldAt",
                table: "SpoilageRecords");

            migrationBuilder.DropColumn(
                name: "SoldQuantity",
                table: "SpoilageRecords");

            migrationBuilder.DropColumn(
                name: "SoldRevenue",
                table: "SpoilageRecords");

            migrationBuilder.DropColumn(
                name: "SpoilageType",
                table: "SpoilageRecords");
        }
    }
}
