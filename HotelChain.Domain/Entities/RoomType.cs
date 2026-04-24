namespace HotelChain.Domain.Entities;

/// <summary>
/// Representa un tipo de habitación dentro del sistema.
/// </summary>
/// <remarks>
/// Permite clasificar habitaciones por categoría, por ejemplo:
/// Double, Junior Suite, Suite o Gran Suite.
/// </remarks>
public class RoomType
{
    /// <summary>
    /// Identificador interno del tipo de habitación.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Nombre del tipo de habitación.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Habitaciones físicas asociadas a este tipo.
    /// </summary>
    public List<Room> Rooms { get; set; } = new();

    /// <summary>
    /// Inventarios comerciales por tipo de habitación.
    /// </summary>
    public List<RoomTypeInventory> Inventories { get; set; } = new();
}
