using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrMcp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdjudicationAndRemoteFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdjudicationType",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "FinancialDisclosure",
                table: "Positions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RemoteEligible",
                table: "Positions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdjudicationType",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "FinancialDisclosure",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "RemoteEligible",
                table: "Positions");
        }
    }
}
