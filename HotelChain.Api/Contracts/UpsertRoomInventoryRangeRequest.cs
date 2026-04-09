namespace HotelChain.Api.Contracts;

public class UpsertRoomInventoryRangeRequest
{
    public int RoomId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int QuantityTotal { get; set; }
}