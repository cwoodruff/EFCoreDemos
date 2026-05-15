using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace migrations_workflow.Migrations
{
    /// <inheritdoc />
    public partial class AddPublishedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "Article",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Article");
        }
    }
}
