using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelChain.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialAvailabilityRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ClosedToArrival",
                table: "RoomTypeInventories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ClosedToDeparture",
                table: "RoomTypeInventories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "RoomTypeInventories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxLengthOfStay",
                table: "RoomTypeInventories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinLengthOfStay",
                table: "RoomTypeInventories",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedToArrival",
                table: "RoomTypeInventories");

            migrationBuilder.DropColumn(
                name: "ClosedToDeparture",
                table: "RoomTypeInventories");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "RoomTypeInventories");

            migrationBuilder.DropColumn(
                name: "MaxLengthOfStay",
                table: "RoomTypeInventories");

            migrationBuilder.DropColumn(
                name: "MinLengthOfStay",
                table: "RoomTypeInventories");
        }
    }
}
