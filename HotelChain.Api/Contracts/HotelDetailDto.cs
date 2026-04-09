namespace HotelChain.Api.Contracts;

/// <summary>
/// DTO utilizado para exponer el detalle completo de un hotel al frontend público.
/// </summary>
public class HotelDetailDto
{
    /// <summary>
    /// Identificador interno del hotel.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Código único del hotel.
    /// </summary>
    public string Code { get; set; } = null!;

    /// <summary>
    /// Nombre comercial del hotel.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Dirección física del hotel.
    /// </summary>
    public string Address { get; set; } = null!;

    /// <summary>
    /// Descripción general del hotel.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Nombre de la ciudad donde se ubica el hotel.
    /// </summary>
    public string City { get; set; } = null!;

    /// <summary>
    /// Calificación promedio calculada a partir de las reseñas registradas.
    /// </summary>
    public decimal AverageRating { get; set; }

    /// <summary>
    /// Cantidad total de reseñas publicadas para el hotel.
    /// </summary>
    public int ReviewsCount { get; set; }

    /// <summary>
    /// Opciones de habitación disponibles para mostrar en el detalle del hotel.
    /// </summary>
    public List<HotelRoomOptionDto> RoomOptions { get; set; } = new();

    /// <summary>
    /// Listado de reseñas asociadas al hotel.
    /// </summary>
    public List<HotelReviewDto> Reviews { get; set; } = new();
}

/// <summary>
/// DTO que representa una opción de habitación mostrada dentro del detalle del hotel.
/// </summary>
public class HotelRoomOptionDto
{
    /// <summary>
    /// Identificador de la habitación.
    /// </summary>
    public int RoomId { get; set; }

    /// <summary>
    /// Nombre o número visible de la habitación.
    /// </summary>
    public string NameOrNumber { get; set; } = null!;

    /// <summary>
    /// Cantidad máxima de huéspedes admitidos.
    /// </summary>
    public int MaxGuests { get; set; }

    /// <summary>
    /// Precio base por noche para la habitación.
    /// </summary>
    public decimal BasePricePerNight { get; set; }

    /// <summary>
    /// Identificador del tipo de habitación.
    /// </summary>
    public int RoomTypeId { get; set; }

    /// <summary>
    /// Nombre del tipo de habitación.
    /// </summary>
    public string RoomType { get; set; } = null!;
}