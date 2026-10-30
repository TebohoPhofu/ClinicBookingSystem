using FluentValidation;

namespace ClinicBookingSystem.BlazorWasm.Services;

/// <summary>Adapts a FluentValidation validator to MudBlazor's per-field validation callback.</summary>
public sealed class FormValidator<T>(IValidator<T> validator)
{
    public Func<object, string, Task<IEnumerable<string>>> ValidateValue => async (model, propertyName) =>
    {
        var context = ValidationContext<T>.CreateWithOptions((T)model, o => o.IncludeProperties(propertyName));
        var result = await validator.ValidateAsync(context);
        return result.IsValid ? [] : result.Errors.Select(e => e.ErrorMessage);
    };

    /// <summary>Validates the whole model; used to gate submit independently of the form's per-field state.</summary>
    public async Task<bool> IsValidAsync(T model) => (await validator.ValidateAsync(model)).IsValid;
}
