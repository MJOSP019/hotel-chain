namespace HotelChain.Api.DTOs.Admin;

public class AdminRoomDto
{
    public int Id { get; set; }

    public int HotelId { get; set; }
    public string HotelName { get; set; } = string.Empty;

    public int RoomTypeId { get; set; }
    public string RoomTypeName { get; set; } = string.Empty;

    public string NameOrNumber { get; set; } = string.Empty;
    public int MaxGuests { get; set; }
    public decimal BasePricePerNight { get; set; }
    public bool IsActive { get; set; }
}