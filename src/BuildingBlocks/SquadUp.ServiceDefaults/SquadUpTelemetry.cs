using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SquadUp.ServiceDefaults;

public static class SquadUpTelemetry
{
    public const string ActivitySourceName = "SquadUp";
    public const string MeterName = "SquadUp";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);
}
