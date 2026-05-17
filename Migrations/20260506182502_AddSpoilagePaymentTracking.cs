using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRLFruitstandESS.Migrations
{
    /// <inheritdoc />
    public partial class AddSpoilagePaymentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add PaymentMethod column to SpoilageRecords
            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "SpoilageRecords",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            // Add SpoilageRecordId column to PaymentTransactions
            migrationBuilder.AddColumn<int>(
                name: "SpoilageRecordId",
                table: "PaymentTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_SpoilageRecordId",
                table: "PaymentTransactions",
                column: "SpoilageRecordId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactions_SpoilageRecords_SpoilageRecordId",
                table: "PaymentTransactions",
                column: "SpoilageRecordId",
                principalTable: "SpoilageRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_SpoilageRecords_SpoilageRecordId",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_SpoilageRecordId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "SpoilageRecordId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "SpoilageRecords");
        }
    }
}
