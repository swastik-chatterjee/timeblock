using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TimeBlock.Core.Entities;

namespace TimeBlock.Api.Services;

public class TokenService(IConfiguration config)
{
    public string CreateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
            new Claim(JwtRegisteredClaimNames.Email, user.Email)
        };
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Issuer,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string SigningKey => config["Jwt:Key"] ?? "dev-only-insecure-timeblock-signing-key-0123456789abcdef";

    public string Issuer => config["Jwt:Issuer"] ?? "timeblock";
}
