namespace HotelChain.Api.Contracts;

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