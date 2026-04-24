namespace HotelChain.Domain.Entities;

public class ReservationCharge
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;
    public string Category { get; set; } = "OTHER";
    public string Description { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public bool IsVoided { get; set; }
    public bool IsSettled { get; set; }
    public DateTime? SettledAt { get; set; }
    public string? SettledBy { get; set; }
}
