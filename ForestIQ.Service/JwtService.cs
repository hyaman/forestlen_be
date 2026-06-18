using ForestIQ.Domain;
using ForestIQ.Domain.Interface;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace ForestIQ.Service
{
    public class JwtService : IJwtService
    {
        public string GenerateToken(string connectionId)
        {
            var expires = DateTime.Today.AddDays(1).AddSeconds(-1);

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Runtime.Jwt.Key));

            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("connectionId", connectionId)
            };

            var token = new JwtSecurityToken(
                issuer: Runtime.Jwt.Issuer,
                audience: Runtime.Jwt.Audience,
                claims: claims,
                expires: expires.ToUniversalTime(),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
