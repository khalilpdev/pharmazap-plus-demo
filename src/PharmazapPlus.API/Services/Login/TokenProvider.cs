using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PharmazapPlus.Shared;
using PharmazapPlus.Shared.Models.Core;

namespace PharmazapPlus.API.Services.Login
{
    public class TokenProvider
    {
        private readonly IConfiguration _configuration;

        public TokenProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Token GenerateToken(UsuarioFarmacia usuario)
        {
            var accessToken = GenerateAccessToken(usuario);
            return new Token
            {
                AccessToken = accessToken,
            };
        }

        private string GenerateAccessToken(UsuarioFarmacia usuario)
        {
            var jwtKey = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT key is not configured. Set 'Jwt:Key' in appsettings.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.Nome),
                new Claim(ClaimTypes.Email, usuario.Email),
                new Claim(ClaimTypes.Role, usuario.Role),
                new Claim(Consts.Claims.FarmaciaId, usuario.FarmaciaId.ToString())
            };

            if (usuario.Role == UserRoles.Desenvolvedor)
            {
                claims.Add(new Claim(ClaimTypes.Role, UserRoles.Gestor));
                claims.Add(new Claim(ClaimTypes.Role, UserRoles.Administrador));
                claims.Add(new Claim(ClaimTypes.Role, UserRoles.Atendente));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(8),
                SigningCredentials = credentials,
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"]
            };

            return new JsonWebTokenHandler().CreateToken(tokenDescriptor);
        }
    }
}
