using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDelivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceRequestDisplacedFromRepId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DisplacedFromRepId",
                table: "ServiceRequests",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplacedFromRepId",
                table: "ServiceRequests");
        }
    }
}
