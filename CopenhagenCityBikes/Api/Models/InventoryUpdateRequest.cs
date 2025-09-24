namespace CopenhagenCityBikes.Api.Models
{
    public record InventoryUpdateRequest(string AdminId, string BikeId, int Delta);
}
