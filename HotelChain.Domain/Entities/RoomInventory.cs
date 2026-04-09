namespace HotelChain.Domain.Entities;

public class RoomInventory
{
    public int Id { get; set; }

    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public DateTime Date { get; set; }

    public int QuantityTotal { get; set; }
    public int QuantityReserved { get; set; }
}