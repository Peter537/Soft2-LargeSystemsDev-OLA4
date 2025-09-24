namespace CopenhagenCityBikes.Models
{
    public class Reservation
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string BikeId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
    }
}