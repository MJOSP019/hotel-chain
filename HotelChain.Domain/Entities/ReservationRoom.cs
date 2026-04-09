namespace HotelChain.Domain.Entities;

public class ReservationRoom
{
    public int Id { get; set; }

    public int ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;

    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public decimal PricePerNight { get; set; }
    public int Nights { get; set; }
    public decimal Subtotal { get; set; }
}