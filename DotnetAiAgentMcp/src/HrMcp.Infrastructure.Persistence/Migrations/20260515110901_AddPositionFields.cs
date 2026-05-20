using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrMcp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPositionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnnouncementNumber",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ApplyUri",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DutyLocationState",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HiringPath",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "KeyRequirements",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OccupationalSeriesTitle",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PositionOfferingType",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PositionUri",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ServiceType",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubAgencyName",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TotalOpenings",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnouncementNumber",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "ApplyUri",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "DutyLocationState",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "HiringPath",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "KeyRequirements",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "OccupationalSeriesTitle",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "PositionOfferingType",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "PositionUri",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "ServiceType",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "SubAgencyName",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "TotalOpenings",
                table: "Positions");
        }
    }
}
