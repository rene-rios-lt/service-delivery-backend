using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDelivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRepStateLastHeartbeatAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastHeartbeatAt",
                table: "RepStateRecords",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastHeartbeatAt",
                table: "RepStateRecords");
        }
    }
}
