namespace HotelChain.Api.Contracts;

/// <summary>
/// DTO utilizado para exponer el detalle completo de un hotel al frontend público.
/// </summary>
public class HotelDetailDto
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string? Description { get; set; }
    public string? MainImageUrl { get; set; }
    public string? ZoneInfo { get; set; }
    public string? Amenities { get; set; }
    public string City { get; set; } = null!;
    public decimal AverageRating { get; set; }
    public int ReviewsCount { get; set; }
    public List<HotelRoomOptionDto> RoomOptions { get; set; } = new();
    public List<HotelReviewDto> Reviews { get; set; } = new();
}

/// <summary>
/// DTO comercial para representar una opción reservable por tipo de habitación.
/// </summary>
public class HotelRoomOptionDto
{
    public int RoomTypeId { get; set; }
    public string RoomType { get; set; } = null!;
    public int MaxGuests { get; set; }
    public decimal BasePricePerNight { get; set; }
    public int AvailableUnits { get; set; }
    public string? BedType { get; set; }
    public decimal? AreaSquareMeters { get; set; }
    public string? ShortDescription { get; set; }
    public string? ImageUrl { get; set; }
}
