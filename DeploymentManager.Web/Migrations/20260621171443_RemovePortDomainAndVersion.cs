using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeploymentManager.Web.Migrations
{
    /// <inheritdoc />
    public partial class RemovePortDomainAndVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Domain",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "Port",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Deployments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Domain",
                table: "Servers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Port",
                table: "Servers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Version",
                table: "Deployments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }
    }
}
