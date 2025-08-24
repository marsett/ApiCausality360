using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiCausality360.Migrations
{
    /// <inheritdoc />
    public partial class NuevoCampoEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Descripcion",
                table: "Events",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fuentes",
                table: "Events",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Descripcion",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "Fuentes",
                table: "Events");
        }
    }
}
