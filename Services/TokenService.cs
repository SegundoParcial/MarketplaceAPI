using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MarketplaceAPI.Models;

namespace MarketplaceAPI.Services
{
    public class TokenService
    {
        private readonly SymmetricSecurityKey _key;
        private readonly string _issuer;
        private readonly string _audience;

        public TokenService(IConfiguration config)
        {
            var jwtKey = config["Jwt:Key"] ?? "devkey_change_me";
            _issuer = config["Jwt:Issuer"] ?? "https://marketplaceapi.azurewebsites.net";
            _audience = config["Jwt:Audience"] ?? "https://marketplaceapi.azurewebsites.net";

            _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        }

        public string CreateToken(User user, Guid? companyId)
        {
            var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role)
            };

            if (companyId.HasValue)
                claims.Add(new Claim("companyId", companyId.Value.ToString()));

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}