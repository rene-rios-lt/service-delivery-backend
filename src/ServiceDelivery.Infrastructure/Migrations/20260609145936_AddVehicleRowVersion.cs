using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDelivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Vehicles",
                type: "BLOB",
                rowVersion: true,
                nullable: false,
                defaultValueSql: "randomblob(8)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Vehicles");
        }
    }
}
