using System.ComponentModel.DataAnnotations;

namespace HotelChain.Api.DTOs.Admin;

/// <summary>
/// Solicitud administrativa para crear varias habitaciones físicas del mismo hotel, tipo y tarifa base.
/// </summary>
public class BulkCreateRoomsRequest
{
    [Required]
    public int HotelId { get; set; }

    [Required]
    public int RoomTypeId { get; set; }

    [Range(1, 99999)]
    public int StartNumber { get; set; }

    [Range(1, 99999)]
    public int EndNumber { get; set; }

    [MaxLength(20)]
    public string? Prefix { get; set; }

    [MaxLength(20)]
    public string? Suffix { get; set; }

    [Range(1, 20)]
    public int MaxGuests { get; set; }

    [Range(0.01, 999999)]
    public decimal BasePricePerNight { get; set; }

    [MaxLength(100)]
    public string? BedType { get; set; }

    [Range(0.01, 9999)]
    public decimal? AreaSquareMeters { get; set; }

    [MaxLength(500)]
    public string? ShortDescription { get; set; }

    [MaxLength(1000)]
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Si es verdadero, omite habitaciones del rango que ya existen en el hotel.
    /// </summary>
    public bool SkipExisting { get; set; } = true;
}
