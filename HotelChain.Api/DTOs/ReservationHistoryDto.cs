namespace HotelChain.Api.DTOs;

public class ReservationHistoryDto
{
    public string Code { get; set; } = "";
    public string Hotel { get; set; } = "";
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Guests { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}