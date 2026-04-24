namespace HotelChain.Domain.Entities;

/// <summary>
/// Representa una reseña o comentario realizado por un usuario sobre un hotel.
/// </summary>
public class HotelReview
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;
    public Guid UserId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? ParentReviewId { get; set; }
    public HotelReview? ParentReview { get; set; }
    public ICollection<HotelReview> Replies { get; set; } = new List<HotelReview>();
}
