namespace HotelChain.Api.Contracts;

public class AdminHotelDto
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Description { get; set; } = "";
    public string? MainImageUrl { get; set; }
    public string? ZoneInfo { get; set; }
    public string? Amenities { get; set; }
    public int CityId { get; set; }
    public string CityName { get; set; } = "";
    public string CityCountryCode { get; set; } = "";
    public bool IsActive { get; set; }
}