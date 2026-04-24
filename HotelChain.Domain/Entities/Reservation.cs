namespace HotelChain.Domain.Entities;

/// <summary>
/// Representa una reservación realizada por un usuario dentro del sistema.
/// </summary>
public class Reservation
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public int HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;
    public Guid? UserId { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public int Guests { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "PENDING";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public List<ReservationRoom> Rooms { get; set; } = new();
    public ReservationPayment? Payment { get; set; }
    public List<ReservationCharge> Charges { get; set; } = new();
}
