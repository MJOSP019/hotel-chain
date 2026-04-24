namespace HotelChain.Domain.Entities;

/// <summary>
/// Representa el registro de auditoría de una búsqueda realizada en el sistema.
/// </summary>
/// <remarks>
/// Guarda los parámetros principales de búsqueda de hoteles o habitaciones,
/// incluyendo ciudad, fechas, huéspedes, filtros aplicados, origen de la búsqueda
/// y fecha de creación del registro.
/// </remarks>
public class SearchAudit
{
    /// <summary>
    /// Identificador interno del registro de auditoría.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Identificador de la ciudad consultada.
    /// </summary>
    public int CityId { get; set; }

    /// <summary>
    /// Ciudad asociada a la búsqueda.
    /// </summary>
    public City City { get; set; } = null!;

    /// <summary>
    /// Identificador del usuario que realizó la búsqueda, si existe.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Fecha de check-in utilizada en la búsqueda.
    /// </summary>
    public DateTime CheckIn { get; set; }

    /// <summary>
    /// Fecha de check-out utilizada en la búsqueda.
    /// </summary>
    public DateTime CheckOut { get; set; }

    /// <summary>
    /// Cantidad de huéspedes solicitados.
    /// </summary>
    public int Guests { get; set; }

    /// <summary>
    /// Precio mínimo utilizado como filtro, si aplica.
    /// </summary>
    public decimal? MinPrice { get; set; }

    /// <summary>
    /// Precio máximo utilizado como filtro, si aplica.
    /// </summary>
    public decimal? MaxPrice { get; set; }

    /// <summary>
    /// Tipo de habitación filtrado, si aplica.
    /// </summary>
    public int? RoomTypeId { get; set; }

    /// <summary>
    /// Rating mínimo utilizado como filtro, si aplica.
    /// </summary>
    public double? MinRating { get; set; }

    /// <summary>
    /// Origen de la búsqueda.
    /// </summary>
    /// <remarks>
    /// Valores comunes: WEB o INTEGRATION.
    /// </remarks>
    public string Source { get; set; } = null!;

    /// <summary>
    /// Fecha y hora de creación del registro de auditoría.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}