namespace CopenhagenCityBikes.Models
{
    public class Rental
    {
        public string Id { get; set; } = string.Empty;
        public string ReservationId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? Duration { get; set; }
        public decimal? Fees { get; set; }
    }
}