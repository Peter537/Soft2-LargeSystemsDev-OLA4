namespace CopenhagenCityBikes.Models
{
    public class Rental
    {
        public string Id { get; set; } = string.Empty;

        // Existing reference to original reservation (kept for traceability)
        public string ReservationId { get; set; } = string.Empty;

        // Newly added to satisfy code using rental.BikeId and rental.UserId
        public string BikeId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        // Derived/summary values (optional – can be computed at end)
        public TimeSpan? Duration { get; set; }
        public decimal? Fees { get; set; }
    }
}