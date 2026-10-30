namespace ClinicBookingSystem.Contracts;

/// <summary>
/// Clinics operate in South Africa Standard Time (UTC+02:00, no daylight saving).
/// A fixed offset keeps the API and the Blazor client in agreement without time-zone databases.
/// </summary>
public static class ClinicTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(2);

    /// <summary>The UTC range covering one clinic-local calendar day.</summary>
    public static (DateTimeOffset From, DateTimeOffset To) DayRange(DateOnly date)
    {
        var from = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), Offset);
        return (from, from.AddDays(1));
    }

    public static DateTimeOffset ToClinicTime(DateTimeOffset value) => value.ToOffset(Offset);
}
