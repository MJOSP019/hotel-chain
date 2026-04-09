namespace HotelChain.Domain.Entities;

public class ReservationAudit
{
    public int Id { get; set; }

    public int ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;

    // Ej: "CANCEL"
    public string Action { get; set; } = null!;

    // Para rastrear cambios
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }

    // Opcional (por ahora)
    public string? Reason { get; set; }   // ej: "Cambio de planes"
    public string? Actor { get; set; }    // ej: "PUBLIC", "ADMIN", email, etc.

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}