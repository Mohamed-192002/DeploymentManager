using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeploymentManager.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddServerDeploymentConfigFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IisAppPoolName",
                table: "Servers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "SimulateIis",
                table: "Servers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TargetDirectory",
                table: "Servers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IisAppPoolName",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "SimulateIis",
                table: "Servers");

            migrationBuilder.DropColumn(
                name: "TargetDirectory",
                table: "Servers");
        }
    }
}
