using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelChain.Infrastructure.Data.Migrations
{
    public partial class AddReservationExpiresAt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "Reservations",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "Reservations");
        }
    }
}