using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Presidents.Application.Contracts;
using Presidents.Application.Security;

namespace Presidents.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "HistoriaDosPresidentes";
    public string Audience { get; set; } = "HistoriaDosPresidentes";
    public string Key { get; set; } = "";
    public int ExpirationMinutes { get; set; } = 480;
}

public sealed class AuthService(UserManager<AppUser> users, SignInManager<AppUser> signIn, IOptions<JwtOptions> jwt) : IAuthService
{
    public async Task<LoginResultDto?> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return null;

        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null)
            return null;

        var result = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return null;

        var roles = await users.GetRolesAsync(user);
        var key = jwt.Value.Key;
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("Jwt__Key ausente ou curta demais. Use pelo menos 32 bytes.");

        var expires = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(jwt.Value.ExpirationMinutes, 15, 1440));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? email),
            new(ClaimTypes.Name, user.Email ?? email)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: jwt.Value.Issuer,
            audience: jwt.Value.Audience,
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));

        return new LoginResultDto(new JwtSecurityTokenHandler().WriteToken(token), expires, user.Email ?? email, roles.ToList());
    }
}
