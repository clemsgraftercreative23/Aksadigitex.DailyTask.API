using Microsoft.AspNetCore.Mvc;
using Infrastructure;
using Isopoh.Cryptography.Argon2;

namespace API;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("login")]
    public IActionResult Login(LoginRequest req)
    {
        var user = _db.Users.FirstOrDefault(x => x.Email == req.Email && x.IsActive);

        if (user == null)
            return Unauthorized();

        if (!VerifyPassword(req.Password, user.PasswordHash))
            return Unauthorized();

        if (user.MfaEnabled == true)
            return Ok(new { mfaRequired = true });

        return Ok(new
        {
            success = true,
            userId = user.Id
        });
    }

    private bool VerifyPassword(string input, string hash)
    {
        if (hash.StartsWith("$2a$") || hash.StartsWith("$2b$"))
            return BCrypt.Net.BCrypt.Verify(input, hash);

        if (hash.StartsWith("$argon2"))
            return Argon2.Verify(hash, input);

        return false;
    }
}

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}