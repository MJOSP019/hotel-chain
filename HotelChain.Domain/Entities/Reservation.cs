namespace HotelChain.Domain.Entities;

/// <summary>
/// Representa una reservación realizada por un usuario dentro del sistema.
/// </summary>
/// <remarks>
/// La reservación almacena el hotel asociado, el usuario que la creó,
/// fechas de estancia, número de huéspedes, monto total, estado actual,
/// habitaciones reservadas y pago asociado.
/// </remarks>
public class Reservation
{
    /// <summary>
    /// Identificador interno de la reservación.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Código único de la reservación.
    /// </summary>
    public string Code { get; set; } = null!;

    /// <summary>
    /// Identificador del hotel asociado a la reservación.
    /// </summary>
    public int HotelId { get; set; }

    /// <summary>
    /// Hotel asociado a la reservación.
    /// </summary>
    public Hotel Hotel { get; set; } = null!;

    /// <summary>
    /// Identificador del usuario que realizó la reservación.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Fecha de check-in de la reservación.
    /// </summary>
    public DateTime CheckIn { get; set; }

    /// <summary>
    /// Fecha de check-out de la reservación.
    /// </summary>
    public DateTime CheckOut { get; set; }

    /// <summary>
    /// Cantidad de huéspedes incluidos en la reservación.
    /// </summary>
    public int Guests { get; set; }

    /// <summary>
    /// Monto total calculado para la reservación.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Estado actual de la reservación, por ejemplo PENDING, CONFIRMED o CANCELED.
    /// </summary>
    public string Status { get; set; } = "CONFIRMED";

    /// <summary>
    /// Fecha y hora de creación del registro.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Habitaciones incluidas dentro de la reservación.
    /// </summary>
    public List<ReservationRoom> Rooms { get; set; } = new();

    /// <summary>
    /// Pago asociado a la reservación, si existe.
    /// </summary>
    public ReservationPayment? Payment { get; set; }
}