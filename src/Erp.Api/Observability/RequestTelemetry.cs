using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace KaguERP.Api.Observability;

internal static class RequestTelemetry
{
    private static readonly Meter Meter = new("KaguERP.Api", "1.0.0");
    private static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>("kagu_erp.api.requests");
    private static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>("kagu_erp.api.request.duration", "ms");

    public static void Record(string method, string route, int statusCode, double elapsedMilliseconds)
    {
        TagList tags = default;
        tags.Add("http.request.method", method);
        tags.Add("http.route", route);
        tags.Add("http.response.status_code", statusCode);
        RequestCounter.Add(1, tags);
        RequestDuration.Record(elapsedMilliseconds, tags);
    }
}
