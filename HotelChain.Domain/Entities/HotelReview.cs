namespace HotelChain.Domain.Entities;

public class HotelReview
{
    public int Id { get; set; }

    public int HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public Guid UserId { get; set; }

    public int Rating { get; set; }
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }
}