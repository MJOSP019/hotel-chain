namespace HotelChain.Domain.Entities;

/// <summary>
/// Representa una habitación registrada dentro de un hotel.
/// </summary>
/// <remarks>
/// La habitación contiene datos básicos como nombre o número identificador,
/// capacidad máxima de huéspedes, precio base por noche, estado activo
/// y sus relaciones con hotel y tipo de habitación.
/// </remarks>
public class Room
{
    /// <summary>
    /// Identificador interno de la habitación.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Nombre o número identificador de la habitación.
    /// </summary>
    public string NameOrNumber { get; set; } = null!;

    /// <summary>
    /// Cantidad máxima de huéspedes permitidos en la habitación.
    /// </summary>
    public int MaxGuests { get; set; }

    /// <summary>
    /// Precio base por noche para la habitación.
    /// </summary>
    public decimal BasePricePerNight { get; set; }

    /// <summary>
    /// Tipo de cama principal de la habitación.
    /// </summary>
    public string? BedType { get; set; }

    /// <summary>
    /// Tamaño aproximado de la habitación en metros cuadrados.
    /// </summary>
    public decimal? AreaSquareMeters { get; set; }

    /// <summary>
    /// Descripción corta comercial de la habitación.
    /// </summary>
    public string? ShortDescription { get; set; }

    /// <summary>
    /// URL o ruta de referencia visual para la habitación.
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Indica si la habitación está activa para búsquedas y reservaciones.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Identificador del hotel al que pertenece la habitación.
    /// </summary>
    public int HotelId { get; set; }

    /// <summary>
    /// Hotel al que pertenece la habitación.
    /// </summary>
    public Hotel Hotel { get; set; } = null!;

    /// <summary>
    /// Identificador del tipo de habitación.
    /// </summary>
    public int RoomTypeId { get; set; }

    /// <summary>
    /// Tipo de habitación asociado.
    /// </summary>
    public RoomType RoomType { get; set; } = null!;
}
