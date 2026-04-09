namespace HotelChain.Domain.Entities;

public class City
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string CountryCode { get; set; } = null!; // "GT", "US", etc.

    public List<Hotel> Hotels { get; set; } = new();
}