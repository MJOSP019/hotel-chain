namespace HotelChain.Api.Contracts;

public class SaveHotelRequest
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string? Description { get; set; }
    public string? MainImageUrl { get; set; }
    public string? ZoneInfo { get; set; }
    public string? Amenities { get; set; }
    public int CityId { get; set; }
}   