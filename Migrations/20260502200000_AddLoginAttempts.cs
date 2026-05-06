using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRLFruitstandESS.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoginAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserName    = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IpAddress   = table.Column<string>(type: "varchar(45)",  maxLength: 45,  nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserAgent   = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Succeeded   = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FailReason  = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AttemptedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginAttempts", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Index for fast lookups by IP and timestamp (used by security dashboard)
            migrationBuilder.CreateIndex(
                name: "IX_LoginAttempts_IpAddress_AttemptedAt",
                table: "LoginAttempts",
                columns: new[] { "IpAddress", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttempts_AttemptedAt",
                table: "LoginAttempts",
                column: "AttemptedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "LoginAttempts");
        }
    }
}
