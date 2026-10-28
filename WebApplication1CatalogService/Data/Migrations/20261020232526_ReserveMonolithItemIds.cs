using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CatalogService.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReserveMonolithItemIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Items",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Items",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Items",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.InsertData(
                table: "Items",
                columns: new[] { "Id", "CategoryId", "CreatedAt", "Description", "IsActive", "Price", "Title" },
                values: new object[,]
                {
                    { 1001, 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "НСК Олімпійський", true, 1500m, "Концерт Океан Ельзи" },
                    { 1002, 2, new DateTime(2025, 1, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Класична постановка", true, 450m, "Гамлет" },
                    { 1003, 3, new DateTime(2025, 1, 3, 0, 0, 0, 0, DateTimeKind.Utc), "УПЛ", true, 800m, "Динамо — Шахтар" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Items",
                keyColumn: "Id",
                keyValue: 1001);

            migrationBuilder.DeleteData(
                table: "Items",
                keyColumn: "Id",
                keyValue: 1002);

            migrationBuilder.DeleteData(
                table: "Items",
                keyColumn: "Id",
                keyValue: 1003);

            migrationBuilder.InsertData(
                table: "Items",
                columns: new[] { "Id", "CategoryId", "CreatedAt", "Description", "IsActive", "Price", "Title" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "НСК Олімпійський", true, 1500m, "Концерт Океан Ельзи" },
                    { 2, 2, new DateTime(2025, 1, 2, 0, 0, 0, 0, DateTimeKind.Utc), "Класична постановка", true, 450m, "Гамлет" },
                    { 3, 3, new DateTime(2025, 1, 3, 0, 0, 0, 0, DateTimeKind.Utc), "УПЛ", true, 800m, "Динамо — Шахтар" }
                });
        }
    }
}
