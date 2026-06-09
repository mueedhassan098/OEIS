using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OEIS.Application.DTOs;
using OEIS.Application.Services;
using OEIS.Api.Security;
using OEIS.Infrastructure.Data;

namespace OEIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly OeisDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly INotificationService _notifications;

    public AuthController(
        OeisDbContext context,
        IConfiguration configuration,
        INotificationService notifications)
    {
        _context = context;
        _configuration = configuration;
        _notifications = notifications;
    }

    // POST: api/auth/login
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
        {
            return BadRequest("Email and password are required.");
        }

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        var user = await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Email.ToLower() == normalizedEmail);

        if (user is null)
        {
            return Unauthorized("Invalid credentials.");
        }

        if (!PasswordUtil.Verify(user.PasswordHash, dto.Password))
        {
            return Unauthorized("Invalid credentials.");
        }

        if (string.Equals(user.RegistrationStatus, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized("Your account is under review. Please wait for admin approval.");
        }

        if (string.Equals(user.RegistrationStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized("Your registration request has been rejected. Please contact admin.");
        }

        var token = GenerateJwtToken(user.EmployeeId, user.Email, user.Role);

        var response = new LoginResponseDto(
            user.EmployeeId,
            user.FullName,
            user.Email,
            user.Role,
            token,
            user.ProfileImageUrl);

        return Ok(response);
    }

    // POST: api/auth/forgot-password
    [HttpPost("forgot-password")]
    public async Task<ActionResult<ForgotPasswordResponseDto>> ForgotPassword([FromBody] ForgotPasswordRequestDto dto)
    {
        const string genericMessage =
            "If an account exists for that email, a password reset link has been sent.";

        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            return BadRequest("Email is required.");
        }

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        var user = await _context.Employees
            .FirstOrDefaultAsync(e => e.Email.ToLower() == normalizedEmail);

        if (user is null)
        {
            return Ok(new ForgotPasswordResponseDto(genericMessage, null));
        }

        if (string.Equals(user.RegistrationStatus, "Pending", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.RegistrationStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new ForgotPasswordResponseDto(genericMessage, null));
        }

        var resetToken = GenerateResetToken();
        var expiryMinutes = _configuration.GetValue("PasswordReset:TokenExpiryMinutes", 60);
        user.PasswordResetToken = resetToken;
        user.PasswordResetTokenExpiresUtc = DateTime.UtcNow.AddMinutes(expiryMinutes);

        await _context.SaveChangesAsync();

        var resetLink = BuildResetLink(user.Email, resetToken);
        await _notifications.SendPasswordResetAsync(user.Email, user.Phone, resetLink);

        return Ok(new ForgotPasswordResponseDto(genericMessage, resetLink));
    }

    // POST: api/auth/reset-password
    [HttpPost("reset-password")]
    public async Task<ActionResult<MessageResponseDto>> ResetPassword([FromBody] ResetPasswordRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.Token) ||
            string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            return BadRequest("Email, token, and new password are required.");
        }

        if (dto.NewPassword.Length < 6)
        {
            return BadRequest("Password must be at least 6 characters.");
        }

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        var token = dto.Token.Trim();

        var user = await _context.Employees
            .FirstOrDefaultAsync(e => e.Email.ToLower() == normalizedEmail);

        if (user is null ||
            string.IsNullOrWhiteSpace(user.PasswordResetToken) ||
            user.PasswordResetTokenExpiresUtc is null ||
            user.PasswordResetTokenExpiresUtc <= DateTime.UtcNow ||
            !FixedTimeEquals(user.PasswordResetToken, token))
        {
            return BadRequest("Invalid or expired reset link. Please request a new password reset.");
        }

        user.PasswordHash = PasswordUtil.Hash(dto.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresUtc = null;

        await _context.SaveChangesAsync();

        return Ok(new MessageResponseDto("Your password has been reset. You can sign in with your new password."));
    }

    private string BuildResetLink(string email, string resetToken)
    {
        var frontendBase = _configuration.GetValue<string>("PasswordReset:FrontendBaseUrl")
            ?? "https://localhost:7015";
        frontendBase = frontendBase.TrimEnd('/');

        return $"{frontendBase}/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(resetToken)}";
    }

    private static string GenerateResetToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private string GenerateJwtToken(int employeeId, string email, string role)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var issuer = jwtSection.GetValue<string>("Issuer")!;
        var audience = jwtSection.GetValue<string>("Audience")!;
        var key = jwtSection.GetValue<string>("Key")!;

        var keyBytes = Encoding.UTF8.GetBytes(key);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, employeeId.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
