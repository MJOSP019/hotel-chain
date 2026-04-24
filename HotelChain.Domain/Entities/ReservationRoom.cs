namespace HotelChain.Domain.Entities;

public class ReservationRoom
{
    public int Id { get; set; }

    public int ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;

    /// <summary>
    /// Tipo de habitación reservado comercialmente.
    /// </summary>
    public int RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;

    /// <summary>
    /// Habitación física asignada operativamente.
    /// Debe poder quedar nula al crear la reserva y llenarse después, por ejemplo en check-in.
    /// </summary>
    public int? RoomId { get; set; }
    public Room? Room { get; set; }

    public decimal PricePerNight { get; set; }
    public int Nights { get; set; }
    public decimal Subtotal { get; set; }
}
