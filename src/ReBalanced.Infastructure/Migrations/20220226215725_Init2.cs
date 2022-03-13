using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReBalanced.Infastructure.Migrations
{
    public partial class Init2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Ticker",
                table: "Holding",
                newName: "AssetId");

            migrationBuilder.CreateTable(
                name: "Asset",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ticker = table.Column<string>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asset", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Holding_AssetId",
                table: "Holding",
                column: "AssetId");

            migrationBuilder.AddForeignKey(
                name: "FK_Holding_Asset_AssetId",
                table: "Holding",
                column: "AssetId",
                principalTable: "Asset",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Holding_Asset_AssetId",
                table: "Holding");

            migrationBuilder.DropTable(
                name: "Asset");

            migrationBuilder.DropIndex(
                name: "IX_Holding_AssetId",
                table: "Holding");

            migrationBuilder.RenameColumn(
                name: "AssetId",
                table: "Holding",
                newName: "Ticker");
        }
    }
}
