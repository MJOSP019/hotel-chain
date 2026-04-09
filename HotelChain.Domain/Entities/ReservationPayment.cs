namespace HotelChain.Domain.Entities;

public class ReservationPayment
{
    public int Id { get; set; }

    public int ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;

    public string Last4 { get; set; } = null!;
    public string CardHolderName { get; set; } = null!;
    public string BillingAddress { get; set; } = null!;

    public string Status { get; set; } = "APPROVED"; // o DECLINED
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}