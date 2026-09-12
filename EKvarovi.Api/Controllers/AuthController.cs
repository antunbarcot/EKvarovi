using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EKvarovi.Api.Data;
using EKvarovi.Shared.DTOs;
using EKvarovi.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace EKvarovi.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string GenericLoginError = "Neispravni podaci za prijavu.";
    private const int TokenLifetimeHours = 8;

    private readonly EKvaroviDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(EKvaroviDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto dto)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var user = await _context.AppUsers
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.AppRole)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

        // Namjerno ista generička poruka za "korisnik ne postoji", "neaktivan" i "kriva lozinka" -
        // ne otkrivamo napadaču postoji li email u sustavu.
        if (user is null || !user.IsActive)
        {
            return Unauthorized(GenericLoginError);
        }

        var hasher = new PasswordHasher<AppUser>();
        var verificationResult = hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized(GenericLoginError);
        }

        return Ok(BuildLoginResponse(user));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<LoginResponseDto>> Me()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await _context.AppUsers
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.AppRole)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        return Ok(BuildLoginResponse(user));
    }

    // Vraca svjez token (isti 8h zivotni vijek kao login) umjesto praznog/ponovljenog -
    // GET /me tako uz provjeru valjanosti odmah djeluje i kao lagani "refresh" bez posebnog endpointa.
    private LoginResponseDto BuildLoginResponse(AppUser user)
    {
        var roles = user.UserRoles
            .Where(ur => ur.AppRole is not null)
            .Select(ur => ur.AppRole!.Name)
            .ToList();

        var expiresAt = DateTime.UtcNow.AddHours(TokenLifetimeHours);
        var token = GenerateToken(user, roles, expiresAt);

        return new LoginResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            UserId = user.Id,
            Email = user.Email,
            DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName,
            Roles = roles,
            EmployeeId = user.EmployeeId
        };
    }

    private string GenerateToken(AppUser user, List<string> roles, DateTime expiresAt)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var key = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key nije konfiguriran.");
        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        if (user.EmployeeId.HasValue)
        {
            claims.Add(new Claim("EmployeeId", user.EmployeeId.Value.ToString()));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
