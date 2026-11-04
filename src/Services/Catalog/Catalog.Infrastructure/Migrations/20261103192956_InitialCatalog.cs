using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.CheckConstraint("CK_Product_Currency", "\"Currency\" IN ('TRY', 'USD', 'EUR')");
                    table.CheckConstraint("CK_Product_Description", "length(btrim(\"Description\")) BETWEEN 1 AND 4000");
                    table.CheckConstraint("CK_Product_Name", "length(btrim(\"Name\")) BETWEEN 1 AND 200");
                    table.CheckConstraint("CK_Product_Price", "\"Price\" > 0");
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "Currency", "Description", "Name", "Price" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "TRY", "Kablolu klavye", "Klavye", 750m },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "TRY", "Optik fare", "Fare", 350m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
