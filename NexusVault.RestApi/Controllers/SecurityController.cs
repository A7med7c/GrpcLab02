using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace NexusVault.RestApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SecurityController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public SecurityController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpPost("authenticate")]
    public IActionResult Authenticate([FromBody] CredentialPayload request)
    {
        if (request.Username == "admin" && request.Password == "password")
        {
            var token = IssueAccessToken(request.Username, "Admin");
            return Ok(new { Token = token, Role = "Admin" });
        }
        else if (request.Username == "user" && request.Password == "password")
        {
            var token = IssueAccessToken(request.Username, "User");
            return Ok(new { Token = token, Role = "User" });
        }

        return Unauthorized("Invalid credentials.");
    }

    private string IssueAccessToken(string username, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "This_is_a_very_secret_key_used_for_demo_123456"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class CredentialPayload
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
