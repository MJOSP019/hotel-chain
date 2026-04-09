namespace HotelChain.Domain.Entities;

/// <summary>
/// Representa un hotel perteneciente a la cadena hotelera.
/// </summary>
/// <remarks>
/// Un hotel contiene información general como código, nombre, dirección, ciudad,
/// estado activo y sus colecciones relacionadas de habitaciones y reseñas.
/// </remarks>
public class Hotel
{
    /// <summary>
    /// Identificador interno del hotel.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Código único del hotel, por ejemplo "HGT001".
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
    public string? Description { get; set; } = "";

    /// <summary>
    /// Indica si el hotel se encuentra activo para búsquedas y operaciones.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Identificador de la ciudad a la que pertenece el hotel.
    /// </summary>
    public int CityId { get; set; }

    /// <summary>
    /// Ciudad asociada al hotel.
    /// </summary>
    public City City { get; set; } = null!;

    /// <summary>
    /// Colección de habitaciones registradas para el hotel.
    /// </summary>
    public ICollection<Room> Rooms { get; set; } = new List<Room>();

    /// <summary>
    /// Colección de reseñas registradas para el hotel.
    /// </summary>
    public ICollection<HotelReview> Reviews { get; set; } = new List<HotelReview>();
}