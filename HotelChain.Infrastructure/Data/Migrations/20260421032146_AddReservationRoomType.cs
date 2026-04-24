using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelChain.Infrastructure.Data.Migrations
{
    public partial class AddReservationRoomType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoomTypeId",
                table: "ReservationRooms",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE rr
                SET rr.RoomTypeId = r.RoomTypeId
                FROM ReservationRooms rr
                INNER JOIN Rooms r ON r.Id = rr.RoomId
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM ReservationRooms
                    WHERE RoomTypeId IS NULL
                )
                BEGIN
                    THROW 50000, 'Hay registros en ReservationRooms que no pudieron mapearse a RoomTypeId.', 1;
                END
            ");

            migrationBuilder.AlterColumn<int>(
                name: "RoomTypeId",
                table: "ReservationRooms",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReservationRooms_RoomTypeId",
                table: "ReservationRooms",
                column: "RoomTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReservationRooms_RoomTypes_RoomTypeId",
                table: "ReservationRooms",
                column: "RoomTypeId",
                principalTable: "RoomTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReservationRooms_RoomTypes_RoomTypeId",
                table: "ReservationRooms");

            migrationBuilder.DropIndex(
                name: "IX_ReservationRooms_RoomTypeId",
                table: "ReservationRooms");

            migrationBuilder.DropColumn(
                name: "RoomTypeId",
                table: "ReservationRooms");
        }
    }
}