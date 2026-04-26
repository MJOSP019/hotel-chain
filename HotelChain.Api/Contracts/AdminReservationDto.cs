namespace HotelChain.Api.Contracts;

public class AdminReservationDto
{
    public int Id { get; set; }
    public int HotelId { get; set; }
    public string Code { get; set; } = "";
    public string Hotel { get; set; } = "";
    public string? RoomType { get; set; }
    public string? AssignedRoomName { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Nights { get; set; }
    public int Guests { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal ChargesTotal { get; set; }
    public decimal SettledChargesTotal { get; set; }
    public decimal OutstandingChargesTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public bool IsStayAccountSettled { get; set; }
    public int ChargeCount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "";
    public string? PaymentStatus { get; set; }
    public bool IsPaid { get; set; }
    public string? CardLast4 { get; set; }
    public Guid? UserId { get; set; }
    public string? GuestFirstName { get; set; }
    public string? GuestLastName { get; set; }
    public string? GuestEmail { get; set; }
    public string? GuestCountry { get; set; }
    public string? GuestPassportNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
