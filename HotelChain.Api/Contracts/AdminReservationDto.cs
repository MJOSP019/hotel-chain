namespace HotelChain.Api.Contracts;

public class AdminReservationDto
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Hotel { get; set; } = "";
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Guests { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "";
    public string PaymentStatus { get; set; } = "";
    public bool IsPaid { get; set; }
    public Guid? UserId { get; set; }
}