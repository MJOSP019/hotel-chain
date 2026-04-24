using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelChain.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHotelReviewReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentReviewId",
                table: "HotelReviews",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HotelReviews_ParentReviewId",
                table: "HotelReviews",
                column: "ParentReviewId");

            migrationBuilder.AddForeignKey(
                name: "FK_HotelReviews_HotelReviews_ParentReviewId",
                table: "HotelReviews",
                column: "ParentReviewId",
                principalTable: "HotelReviews",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HotelReviews_HotelReviews_ParentReviewId",
                table: "HotelReviews");

            migrationBuilder.DropIndex(
                name: "IX_HotelReviews_ParentReviewId",
                table: "HotelReviews");

            migrationBuilder.DropColumn(
                name: "ParentReviewId",
                table: "HotelReviews");
        }
    }
}