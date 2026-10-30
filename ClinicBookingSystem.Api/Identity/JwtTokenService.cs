using System.Security.Claims;
using System.Text;
using ClinicBookingSystem.Contracts;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ClinicBookingSystem.Api.Identity;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "ClinicBookingSystem";
    public string Audience { get; set; } = "ClinicBookingSystem.Client";
    public string Key { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}

public interface ITokenService
{
    AuthResponse CreateToken(AppUser user, IReadOnlyList<string> roles);
}

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    public AuthResponse CreateToken(AppUser user, IReadOnlyList<string> roles)
    {
        var jwt = options.Value;
        var expires = clock.GetUtcNow().AddMinutes(jwt.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, user.FullName)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)), SecurityAlgorithms.HmacSha256);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Expires = expires.UtcDateTime,
            SigningCredentials = credentials
        };

        return new AuthResponse(new JsonWebTokenHandler().CreateToken(descriptor), expires, user.Email!, user.FullName, roles);
    }
}
