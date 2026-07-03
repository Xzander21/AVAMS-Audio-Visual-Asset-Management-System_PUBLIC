using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AVEquipmentManager.API.Data;
using AVEquipmentManager.Shared.DTOs;
using AVEquipmentManager.Shared.Enums;
using AVEquipmentManager.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AVEquipmentManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext  _context;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _config  = config;
    }

    // POST /api/auth/login
    // Finding #6 / CWE-307 patch: throttled to 5 attempts per minute per IP
    //   via the "login" rate-limiting policy declared in Program.cs.
    [HttpPost("login")]
    [AllowAnonymous]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == dto.Username);

        // Finding #5 / CWE-204 / CWE-208 patch (2026-06-16): defeat the login
        // timing oracle. Before this change, when the username did not exist
        // the endpoint returned in <1 ms because BCrypt.Verify was skipped,
        // whereas an existing-user-wrong-password took ~100 ms to BCrypt the
        // stored hash. The latency delta let an attacker enumerate the whole
        // user table by timing alone. We now ALWAYS perform exactly one
        // BCrypt.Verify call so the response time is uniform regardless of
        // which side of the credential pair was invalid.
        //
        // The dummy hash below is a valid BCrypt hash of a random string,
        // baked in at compile time so its cost is identical to verifying a
        // real password (work factor 11 by BCrypt.Net's default).
        const string DummyHash =
            "$2a$11$NB.U1HxbR2sIVuOJ1xSf0OZ.WGE9wF1AvNHTcHvVqI5/AWmgEBVTW";
        bool credentialOk;
        if (user is null)
        {
            // Burn equivalent CPU even when there is no real user to verify.
            _ = BCrypt.Net.BCrypt.Verify(dto.Password, DummyHash);
            credentialOk = false;
        }
        else
        {
            credentialOk = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
        }

        if (!credentialOk || user is null)
            return Unauthorized(new { message = "Invalid username or password." });

        var token  = GenerateJwt(user);
        var expiry = DateTime.UtcNow.AddHours(
            _config.GetValue<int>("Jwt:ExpiryHours", 24));

        return Ok(new LoginResponseDto
        {
            Token    = token,
            Username = user.Username,
            Email    = user.Email,
            Role     = user.Role,
            Expiry   = expiry
        });
    }

    // GET /api/auth/me  — returns current user info from the JWT
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me()
    {
        var username = User.FindFirstValue(ClaimTypes.Name);
        var user     = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null) return NotFound();

        return Ok(new UserDto
        {
            Id        = user.Id,
            Username  = user.Username,
            Email     = user.Email,
            Role      = user.Role,
            CreatedAt = user.CreatedAt
        });
    }

    // GET /api/auth/users  — Admin only: list all users
    [HttpGet("users")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
    {
        var users = await _context.Users
            .OrderBy(u => u.Role)
            .ThenBy(u => u.Username)
            .Select(u => new UserDto
            {
                Id        = u.Id,
                Username  = u.Username,
                Email     = u.Email,
                Role      = u.Role,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    // POST /api/auth/register  — Admin only: create a new user
    [HttpPost("register")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDto>> Register([FromBody] RegisterUserDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // Normalise legacy role strings and validate against the canonical
        // four-role set (Admin, Supervisor, AVStaff, ITSpecialist).
        var normalisedRole = Roles.Normalize(dto.Role);
        if (!Roles.IsValid(normalisedRole))
            return BadRequest(new
            {
                message = $"Role must be one of: {string.Join(", ", Roles.All)}."
            });

        var exists = await _context.Users
            .AnyAsync(u => u.Username == dto.Username || u.Email == dto.Email);
        if (exists)
            return Conflict(new { message = "Username or email already in use." });

        var user = new User
        {
            Username     = dto.Username,
            Email        = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role         = normalisedRole,
            CreatedAt    = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(Me), new UserDto
        {
            Id        = user.Id,
            Username  = user.Username,
            Email     = user.Email,
            Role      = user.Role,
            CreatedAt = user.CreatedAt
        });
    }

    // PUT /api/auth/users/{id}/role  — Admin only: change a user's role
    [HttpPut("users/{id:int}/role")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ChangeRole(int id, [FromBody] ChangeRoleDto dto)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        var normalisedRole = Roles.Normalize(dto.Role);
        if (!Roles.IsValid(normalisedRole))
            return BadRequest(new
            {
                message = $"Role must be one of: {string.Join(", ", Roles.All)}."
            });

        user.Role = normalisedRole;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/auth/users/{id}  — Admin only
    [HttpDelete("users/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var me = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (me != null && int.Parse(me) == id)
            return BadRequest(new { message = "You cannot delete your own account." });

        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ── JWT helper ───────────────────────────────────────────────────────────
    private string GenerateJwt(User user)
    {
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddHours(_config.GetValue<int>("Jwt:ExpiryHours", 24));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,      user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(ClaimTypes.NameIdentifier,        user.Id.ToString()),
            new Claim(ClaimTypes.Name,                  user.Username),
            new Claim(ClaimTypes.Email,                 user.Email),
            new Claim(ClaimTypes.Role,                  user.Role),
            new Claim(JwtRegisteredClaimNames.Jti,      Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:             claims,
            expires:            expiry,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// ── Extra DTOs (auth-only, not shared) ──────────────────────────────────────
public class RegisterUserDto
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string Email    { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    public string Role     { get; set; } = "ITSpecialist";
}

public class ChangeRoleDto
{
    [System.ComponentModel.DataAnnotations.Required]
    public string Role { get; set; } = string.Empty;
}
