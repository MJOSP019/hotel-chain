namespace HotelChain.Web.Models;

public class HotelDetailDto
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string? Description { get; set; }
    public string? MainImageUrl { get; set; }
    public string? ZoneInfo { get; set; }
    public string? Amenities { get; set; }
    public string City { get; set; } = "";
    public decimal AverageRating { get; set; }
    public int ReviewsCount { get; set; }
    public List<HotelRoomOptionDto> RoomOptions { get; set; } = new();
    public List<HotelRestrictionMessageDto> RestrictionMessages { get; set; } = new();
    public List<HotelReviewDto> Reviews { get; set; } = new();
}

public class HotelRestrictionMessageDto
{
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public int AffectedOptions { get; set; }
}

public class HotelRoomOptionDto
{
    public int RoomTypeId { get; set; }
    public string RoomType { get; set; } = "";
    public int MaxGuests { get; set; }
    public decimal BasePricePerNight { get; set; }
    public int AvailableUnits { get; set; }
    public string? BedType { get; set; }
    public decimal? AreaSquareMeters { get; set; }
    public string? ShortDescription { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsBookable { get; set; } = true;
    public string? RestrictionCode { get; set; }
    public string? RestrictionMessage { get; set; }
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
    public int? ParentReviewId { get; set; }
    public List<HotelReviewDto> Replies { get; set; } = new();
}

public class CreateHotelReviewRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public int? ParentReviewId { get; set; }
}