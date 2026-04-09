namespace HotelChain.Domain.Entities;

public class RoomType
{
    public int Id { get; set; }
    public string Name { get; set; } = null!; // Double, Suite, etc.

    public List<Room> Rooms { get; set; } = new();
}