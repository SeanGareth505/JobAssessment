using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;

    public AuthController(IConfiguration config)
    {
        _config = config;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        var mockPassword = _config["Auth:MockPassword"];
        var user = ValidateMockUser(request.UserName?.Trim(), request.Password, mockPassword);
        if (user == null)
            return Unauthorized();

        var token = GenerateJwt(user.Name, user.Roles);
        return Ok(new LoginResponse
        {
            AccessToken = token,
            ExpiresInSeconds = 3600,
            User = new UserInfo { Name = user.Name, Roles = user.Roles }
        });
    }

    private static MockUser? ValidateMockUser(string? userName, string? password, string? expectedPassword)
    {
        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(expectedPassword) || password == null) return null;
        if (!SecureEquals(password, expectedPassword)) return null;
        var users = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["admin"] = ["Orders.Read", "Orders.Write", "Orders.Admin"],
            ["writer"] = ["Orders.Read", "Orders.Write"],
            ["reader"] = ["Orders.Read"],
        };
        if (!users.TryGetValue(userName, out var roles)) return null;
        return new MockUser(userName, roles);
    }

    private static bool SecureEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        if (aBytes.Length != bBytes.Length) return false;
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    private string GenerateJwt(string name, IReadOnlyList<string> roles)
    {
        var key = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key must be configured.");
        var issuer = _config["Jwt:Issuer"] ?? "OrderManagement";
        var audience = _config["Jwt:Audience"] ?? "OrderManagement";
        var expiryMinutes = int.TryParse(_config["Jwt:ExpiryMinutes"], out var m) && m > 0 ? m : 60;

        var keyBytes = Encoding.UTF8.GetBytes(key);
        var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, name),
            new(JwtRegisteredClaimNames.Sub, name),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(expiryMinutes),
            creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record MockUser(string Name, IReadOnlyList<string> Roles);
}
