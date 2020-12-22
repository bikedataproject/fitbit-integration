using Microsoft.EntityFrameworkCore.Migrations;

namespace BikeDataProject.Integrations.Fitbit.Db.Migrations
{
    public partial class LogId64Bit : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "FitBitLogId",
                table: "Contributions",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "FitBitLogId",
                table: "Contributions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
