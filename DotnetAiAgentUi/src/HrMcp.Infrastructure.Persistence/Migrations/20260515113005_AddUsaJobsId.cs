using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrMcp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsaJobsId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UsaJobsId",
                table: "Positions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsaJobsId",
                table: "Positions");
        }
    }
}
