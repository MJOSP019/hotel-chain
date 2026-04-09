namespace HotelChain.Api.Contracts;

public class CreateHotelReviewRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
}