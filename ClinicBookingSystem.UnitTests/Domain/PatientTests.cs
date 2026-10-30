using ClinicBookingSystem.Patients.Domain;
using ClinicBookingSystem.SharedKernel;
using Shouldly;

namespace ClinicBookingSystem.UnitTests.Domain;

public class PatientTests
{
    private static readonly DateOnly Today = new(2030, 1, 1);

    [Fact]
    public void Register_ValidInput_NormalisesEmailAndTrimsNames()
    {
        var patient = Patient.Register(Guid.NewGuid(), " Thabo ", "Mokoena", " Thabo@Mail.COM ", "0821234567", new DateOnly(1990, 5, 1), Today);

        patient.Email.ShouldBe("thabo@mail.com");
        patient.FullName.ShouldBe("Thabo Mokoena");
    }

    [Theory]
    [InlineData("", "Mokoena", "a@b.com")]
    [InlineData("Thabo", "", "a@b.com")]
    [InlineData("Thabo", "Mokoena", "not-an-email")]
    public void Register_InvalidInput_ThrowsDomainException(string first, string last, string email)
    {
        Should.Throw<DomainException>(() =>
            Patient.Register(Guid.NewGuid(), first, last, email, "", new DateOnly(1990, 5, 1), Today));
    }

    [Fact]
    public void Register_FutureDateOfBirth_ThrowsDomainException()
    {
        Should.Throw<DomainException>(() =>
            Patient.Register(Guid.NewGuid(), "A", "B", "a@b.com", "", Today.AddDays(1), Today));
    }
}
