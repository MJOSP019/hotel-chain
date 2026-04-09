namespace HotelChain.Api.Contracts;

public class AdminRoomInventoryDto
{
    public int Id { get; set; }

    public int RoomId { get; set; }
    public string RoomNameOrNumber { get; set; } = string.Empty;

    public int HotelId { get; set; }
    public string HotelName { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    public int QuantityTotal { get; set; }
    public int QuantityReserved { get; set; }
    public int QuantityAvailable { get; set; }
}