using System.Text.Json;
using HotelChain.Web.Models;
using Microsoft.JSInterop;

namespace HotelChain.Web.Services;

public class ReservationCartService
{
    private const string StorageKey = "hotelchain_reservation_cart_v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IJSRuntime _js;

    public event Action? OnChange;

    public ReservationCartService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<List<ReservationCartItem>> GetItemsAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);

            if (string.IsNullOrWhiteSpace(json))
                return new List<ReservationCartItem>();

            return JsonSerializer.Deserialize<List<ReservationCartItem>>(json, JsonOptions)
                   ?? new List<ReservationCartItem>();
        }
        catch
        {
            return new List<ReservationCartItem>();
        }
    }

    public async Task<int> GetCountAsync()
    {
        var items = await GetItemsAsync();
        return items.Sum(x => Math.Max(1, x.Quantity));
    }

    public async Task AddAsync(ReservationCartItem item)
    {
        var items = await GetItemsAsync();

        item.Id = string.IsNullOrWhiteSpace(item.Id)
            ? Guid.NewGuid().ToString("N")
            : item.Id;

        item.Quantity = Math.Max(1, item.Quantity);

        var existing = items.FirstOrDefault(x =>
            x.HotelId == item.HotelId &&
            x.RoomTypeId == item.RoomTypeId &&
            x.CheckIn.Date == item.CheckIn.Date &&
            x.CheckOut.Date == item.CheckOut.Date &&
            x.Guests == item.Guests);

        if (existing is not null)
        {
            var maxAllowed = item.AvailableUnits > 0 ? item.AvailableUnits : 99;
            existing.Quantity = Math.Min(existing.Quantity + item.Quantity, maxAllowed);
            existing.AvailableUnits = item.AvailableUnits;
            existing.PricePerNight = item.PricePerNight;
            existing.ImageUrl = item.ImageUrl;
            existing.BedType = item.BedType;
            existing.AreaSquareMeters = item.AreaSquareMeters;
        }
        else
        {
            items.Add(item);
        }

        await SaveAsync(items);
    }

    public async Task ChangeQuantityAsync(string id, int quantity)
    {
        var items = await GetItemsAsync();
        var item = items.FirstOrDefault(x => x.Id == id);

        if (item is null)
            return;

        var maxAllowed = item.AvailableUnits > 0 ? item.AvailableUnits : 99;
        item.Quantity = Math.Clamp(quantity, 1, maxAllowed);

        await SaveAsync(items);
    }

    public async Task RemoveAsync(string id)
    {
        var items = await GetItemsAsync();
        items.RemoveAll(x => x.Id == id);
        await SaveAsync(items);
    }

    public async Task ClearAsync()
    {
        await SaveAsync(new List<ReservationCartItem>());
    }

    private async Task SaveAsync(List<ReservationCartItem> items)
    {
        var json = JsonSerializer.Serialize(items, JsonOptions);
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        OnChange?.Invoke();
    }
}