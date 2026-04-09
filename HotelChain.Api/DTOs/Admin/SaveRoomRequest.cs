using System.ComponentModel.DataAnnotations;

namespace HotelChain.Api.DTOs.Admin;

public class SaveRoomRequest
{
    [Required]
    public int HotelId { get; set; }

    [Required]
    public int RoomTypeId { get; set; }

    [Required]
    public string NameOrNumber { get; set; } = string.Empty;

    [Range(1, 20)]
    public int MaxGuests { get; set; }

    [Range(0.01, 999999)]
    public decimal BasePricePerNight { get; set; }

    public bool IsActive { get; set; } = true;
}