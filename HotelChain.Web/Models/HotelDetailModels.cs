namespace HotelChain.Web.Models;

public class HotelDetailDto
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string? Description { get; set; }
    public string City { get; set; } = "";
    public decimal AverageRating { get; set; }
    public int ReviewsCount { get; set; }

    public List<HotelRoomOptionDto> RoomOptions { get; set; } = new();
    public List<HotelReviewDto> Reviews { get; set; } = new();
}

public class HotelRoomOptionDto
{
    public int RoomId { get; set; }
    public string NameOrNumber { get; set; } = "";
    public int MaxGuests { get; set; }
    public decimal BasePricePerNight { get; set; }
    public int RoomTypeId { get; set; }
    public string RoomType { get; set; } = "";
}

public class HotelReviewDto
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = "";
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateHotelReviewRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
}