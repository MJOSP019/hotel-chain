namespace HotelChain.Domain.Entities;

public class SearchAudit
{
    public int Id { get; set; }

    public int CityId { get; set; }
    public City City { get; set; } = null!;

    public Guid? UserId { get; set; }

    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Guests { get; set; }

    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? RoomTypeId { get; set; }
    public double? MinRating { get; set; }

    public string Source { get; set; } = null!; // WEB / INTEGRATION

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}