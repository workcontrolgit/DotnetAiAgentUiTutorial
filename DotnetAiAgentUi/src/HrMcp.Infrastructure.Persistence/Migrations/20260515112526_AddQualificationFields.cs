using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrMcp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQualificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Education",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Evaluations",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PromotionPotential",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Education",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "Evaluations",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "PromotionPotential",
                table: "Positions");
        }
    }
}
