using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace migrations_workflow.Migrations
{
    /// <inheritdoc />
    public partial class AddIsbnUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Isbn",
                table: "Article",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Article_Isbn",
                table: "Article",
                column: "Isbn",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Article_Isbn",
                table: "Article");

            migrationBuilder.DropColumn(
                name: "Isbn",
                table: "Article");
        }
    }
}
