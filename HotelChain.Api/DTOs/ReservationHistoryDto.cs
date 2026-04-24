namespace HotelChain.Api.DTOs;

public class ReservationHistoryDto
{
    public int HotelId { get; set; }
    public string Code { get; set; } = "";
    public string Hotel { get; set; } = "";
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Nights { get; set; }
    public int Guests { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal ChargesTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "";
    public bool IsPaid { get; set; }
    public string? PaymentStatus { get; set; }
    public string? CardLast4 { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
