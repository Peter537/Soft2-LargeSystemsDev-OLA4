using NLog;

namespace CopenhagenCityBikes.Helpers
{
    public sealed class CorrelationIdScope : IDisposable
    {
        private readonly string? _previous;
        private bool _disposed;

        private CorrelationIdScope(string id)
        {
            _previous = MappedDiagnosticsLogicalContext.Get("correlation_id");
            MappedDiagnosticsLogicalContext.Set("correlation_id", id);
        }

        public static CorrelationIdScope Push(string id) => new(id);

        public void Dispose()
        {
            if (_disposed) return;
            if (_previous == null)
                MappedDiagnosticsLogicalContext.Remove("correlation_id");
            else
                MappedDiagnosticsLogicalContext.Set("correlation_id", _previous);
            _disposed = true;
        }
    }
}