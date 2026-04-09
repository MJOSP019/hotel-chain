namespace HotelChain.Domain.Entities;

public class Room
{
    public int Id { get; set; }

    public string NameOrNumber { get; set; } = null!;
    public int MaxGuests { get; set; }
    public decimal BasePricePerNight { get; set; }
    public bool IsActive { get; set; } = true;

    public int HotelId { get; set; }
    public Hotel Hotel { get; set; } = null!;

    public int RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;
}