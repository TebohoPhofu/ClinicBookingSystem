using System.Net.Http.Json;
using System.Text.Json;
using ClinicBookingSystem.Contracts;

namespace ClinicBookingSystem.BlazorWasm.Services;

/// <summary>Outcome of an API call: either a value or a user-presentable error message.</summary>
public sealed record ApiResult<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
}

public interface IClinicApi
{
    Task<ApiResult<AuthResponse>> LoginAsync(LoginRequest request);
    Task<ApiResult<AuthResponse>> RegisterAsync(RegisterRequest request);
    Task<ApiResult<IReadOnlyList<ClinicDto>>> GetClinicsAsync();
    Task<ApiResult<IReadOnlyList<TimeSlotDto>>> GetTimeSlotsAsync(Guid clinicId, DateOnly date);
    Task<ApiResult<IReadOnlyList<AppointmentDto>>> GetMyAppointmentsAsync();
    Task<ApiResult<AppointmentConfirmationDto>> BookAsync(Guid timeSlotId);
    Task<ApiResult<AppointmentConfirmationDto>> RescheduleAsync(Guid appointmentId, Guid newTimeSlotId);
    Task<ApiResult<bool>> CancelAsync(Guid appointmentId);
    Task<ApiResult<ClinicDto>> CreateClinicAsync(CreateClinicRequest request);
    Task<ApiResult<GenerateTimeSlotsResponse>> GenerateTimeSlotsAsync(GenerateTimeSlotsRequest request);
}

/// <summary>Typed HttpClient wrapper over the ASP.NET Core minimal API.</summary>
public sealed class ClinicApiClient(HttpClient http) : IClinicApi
{
    public Task<ApiResult<AuthResponse>> LoginAsync(LoginRequest request) =>
        SendAsync<AuthResponse>(() => http.PostAsJsonAsync("api/auth/login", request));

    public Task<ApiResult<AuthResponse>> RegisterAsync(RegisterRequest request) =>
        SendAsync<AuthResponse>(() => http.PostAsJsonAsync("api/auth/register", request));

    public Task<ApiResult<IReadOnlyList<ClinicDto>>> GetClinicsAsync() =>
        SendAsync<IReadOnlyList<ClinicDto>>(() => http.GetAsync("api/clinics"));

    public Task<ApiResult<IReadOnlyList<TimeSlotDto>>> GetTimeSlotsAsync(Guid clinicId, DateOnly date) =>
        SendAsync<IReadOnlyList<TimeSlotDto>>(() => http.GetAsync($"api/clinics/{clinicId}/timeslots?date={date:yyyy-MM-dd}"));

    public Task<ApiResult<IReadOnlyList<AppointmentDto>>> GetMyAppointmentsAsync() =>
        SendAsync<IReadOnlyList<AppointmentDto>>(() => http.GetAsync("api/appointments"));

    public Task<ApiResult<AppointmentConfirmationDto>> BookAsync(Guid timeSlotId) =>
        SendAsync<AppointmentConfirmationDto>(() => http.PostAsJsonAsync("api/appointments", new BookAppointmentRequest(timeSlotId)));

    public Task<ApiResult<AppointmentConfirmationDto>> RescheduleAsync(Guid appointmentId, Guid newTimeSlotId) =>
        SendAsync<AppointmentConfirmationDto>(() =>
            http.PutAsJsonAsync($"api/appointments/{appointmentId}/reschedule", new RescheduleAppointmentRequest(newTimeSlotId)));

    public async Task<ApiResult<bool>> CancelAsync(Guid appointmentId) =>
        await SendAsync<bool>(() => http.DeleteAsync($"api/appointments/{appointmentId}"), noContent: true);

    public Task<ApiResult<ClinicDto>> CreateClinicAsync(CreateClinicRequest request) =>
        SendAsync<ClinicDto>(() => http.PostAsJsonAsync("api/clinics", request));

    public Task<ApiResult<GenerateTimeSlotsResponse>> GenerateTimeSlotsAsync(GenerateTimeSlotsRequest request) =>
        SendAsync<GenerateTimeSlotsResponse>(() => http.PostAsJsonAsync("api/timeslots/generate", request));

    private static async Task<ApiResult<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> call, bool noContent = false)
    {
        try
        {
            using var response = await call();
            if (response.IsSuccessStatusCode)
                return noContent ? new ApiResult<T>(default, null) : new ApiResult<T>(await response.Content.ReadFromJsonAsync<T>(), null);

            return new ApiResult<T>(default, await ReadProblemAsync(response));
        }
        catch (HttpRequestException)
        {
            return new ApiResult<T>(default, "Cannot reach the booking service. Please try again shortly.");
        }
    }

    private static async Task<string> ReadProblemAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return "Invalid credentials or your session has expired.";

        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String) return detail.GetString()!;
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                return string.Join(" ", errors.EnumerateObject().SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString())));
            if (root.TryGetProperty("title", out var title)) return title.GetString() ?? "Request failed.";
        }
        catch (JsonException)
        {
            // Fall through to the generic message.
        }
        return $"Request failed ({(int)response.StatusCode}).";
    }
}
