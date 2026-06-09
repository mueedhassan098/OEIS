using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OEIS.Application.DTOs;
using OEIS.Application.Services;
using OEIS.Api.Security;
using OEIS.Domain.Entities;
using OEIS.Infrastructure.Data;

namespace OEIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly OeisDbContext _context;
    private readonly INotificationService _notifications;
    private readonly IPortalActivityService _portal;
    private readonly IGoogleEmailVerificationService _googleEmail;
    private readonly IProfileImageStorage _profileImages;

    public EmployeesController(
        OeisDbContext context,
        INotificationService notifications,
        IPortalActivityService portal,
        IGoogleEmailVerificationService googleEmail,
        IProfileImageStorage profileImages)
    {
        _context = context;
        _notifications = notifications;
        _portal = portal;
        _googleEmail = googleEmail;
        _profileImages = profileImages;
    }

    // GET: api/employees
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<Employee>>> GetEmployees()
    {
        return await _context.Employees.AsNoTracking().ToListAsync();
    }
    
    // GET: api/employees/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<object>> GetEmployee(int id)
    {
        var employee = await _context.Employees.FindAsync(id);
        if (employee == null) return NotFound();

        var role = User.FindFirstValue(ClaimTypes.Role);
        var currentId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) &&
            (!int.TryParse(currentId, out var callerId) || callerId != id))
        {
            return Forbid();
        }

        return new
        {
            employee.EmployeeId,
            employee.FullName,
            employee.Email,
            employee.Department,
            employee.Salary,
            employee.Role,
            employee.Phone,
            employee.Address,
            employee.ProfileImageUrl
        };
    }

    // POST: api/employees
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Employee>> CreateEmployee([FromBody] EmployeeCreateDto dto)
    {
        // basic server-side validation (FluentValidation can be wired as filter later)
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var exists = await _context.Employees.AnyAsync(e => e.Email == dto.Email);
        if (exists) return Conflict("Email already exists.");

        var employee = new Employee
        {
            EmployeeCode = string.IsNullOrWhiteSpace(dto.EmployeeCode)
                ? null
                : dto.EmployeeCode,
            FullName = dto.FullName,
            Email = dto.Email,
            PasswordHash = PasswordUtil.Hash(dto.Password),
            Department = dto.Department,
            Salary = dto.Salary,
            Role = dto.Role,
            RegistrationStatus = "Approved",
            Phone = dto.Phone,
            Address = dto.Address,
            ProfileImageUrl = dto.ProfileImageUrl,
            AnnualLeaveQuota = dto.AnnualLeaveQuota,
            RemainingLeaves = dto.AnnualLeaveQuota
        };

        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        await _notifications.SendCredentialsAsync(employee.Email, employee.Phone, dto.Password);

        await _portal.RecordAsync(
            "New employee added",
            $"{employee.FullName} joined {employee.Department}.",
            "Success",
            "/home");

        return CreatedAtAction(nameof(GetEmployee), new { id = employee.EmployeeId }, employee);
    }

    // POST: api/employees/verify-google-email
    [HttpPost("verify-google-email")]
    [AllowAnonymous]
    public async Task<ActionResult<VerifyGoogleEmailResponseDto>> VerifyGoogleEmail([FromBody] VerifyGoogleEmailRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            return BadRequest(new VerifyGoogleEmailResponseDto(false, "Email is required."));
        }

        var result = await _googleEmail.VerifyAsync(dto.Email.Trim());
        return Ok(new VerifyGoogleEmailResponseDto(result.IsGoogleEmail, result.Message));
    }

    // POST: api/employees/register
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] EmployeeRegistrationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var googleCheck = await _googleEmail.VerifyAsync(normalizedEmail);
        if (!googleCheck.IsGoogleEmail)
        {
            return BadRequest(googleCheck.Message);
        }

        var exists = await _context.Employees.AnyAsync(e => e.Email.ToLower() == normalizedEmail);
        if (exists) return Conflict("Email already exists.");

        var profileImageUrl = await _profileImages.SaveBase64Async(dto.ProfileImageBase64);

        var employee = new Employee
        {
            FullName = dto.FullName.Trim(),
            Email = normalizedEmail,
            PasswordHash = PasswordUtil.Hash(dto.Password),
            Department = dto.Department.Trim(),
            Role = "Employee",
            RegistrationStatus = "Pending",
            Phone = dto.Phone,
            Address = dto.Address,
            ProfileImageUrl = profileImageUrl,
            AnnualLeaveQuota = 30,
            RemainingLeaves = 30,
            Salary = 0m
        };

        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Registration submitted successfully. Please wait for admin approval.",
            employeeId = employee.EmployeeId
        });
    }

    // GET: api/employees/registration-requests?status=Pending&search=abc
    [HttpGet("registration-requests")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<object>>> GetRegistrationRequests([FromQuery] string? status = "Pending", [FromQuery] string? search = null)
    {
        var query = _context.Employees
            .AsNoTracking()
            .Where(e => e.Role == "Employee");

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(e => e.RegistrationStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e =>
                e.FullName.Contains(search) ||
                e.Email.Contains(search) ||
                e.Department.Contains(search));
        }

        var list = await query
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new
            {
                e.EmployeeId,
                e.FullName,
                e.Email,
                e.Department,
                e.Phone,
                e.Address,
                e.ProfileImageUrl,
                e.CreatedAt,
                e.RegistrationStatus,
                e.RegistrationReviewNote
            })
            .ToListAsync();

        return list;
    }

    // PUT: api/employees/registration-requests/{id}/approve
    [HttpPut("registration-requests/{id:int}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ApproveRegistration(int id, [FromBody] EmployeeRegistrationDecisionDto? dto)
    {
        var employee = await _context.Employees.FindAsync(id);
        if (employee == null) return NotFound();

        employee.RegistrationStatus = "Approved";
        employee.RegistrationReviewNote = string.IsNullOrWhiteSpace(dto?.Note) ? null : dto!.Note!.Trim();
        await _context.SaveChangesAsync();

        await _portal.RecordAsync(
            "Registration approved",
            $"{employee.FullName} can sign in to the portal.",
            "Success",
            "/home");

        return NoContent();
    }

    // PUT: api/employees/registration-requests/{id}/reject
    [HttpPut("registration-requests/{id:int}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RejectRegistration(int id, [FromBody] EmployeeRegistrationDecisionDto? dto)
    {
        var employee = await _context.Employees.FindAsync(id);
        if (employee == null) return NotFound();

        employee.RegistrationStatus = "Rejected";
        employee.RegistrationReviewNote = string.IsNullOrWhiteSpace(dto?.Note) ? "Rejected by admin." : dto!.Note!.Trim();
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // PUT: api/employees/{id}/profile
    [HttpPut("{id:int}/profile")]
    public async Task<IActionResult> UpdateProfile(int id, [FromBody] EmployeeSelfProfileUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Department))
        {
            return BadRequest("Full name and department are required.");
        }

        var employee = await _context.Employees.FindAsync(id);
        if (employee == null)
        {
            return NotFound();
        }

        var role = User.FindFirstValue(ClaimTypes.Role);
        var currentId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) &&
            (!int.TryParse(currentId, out var callerId) || callerId != id))
        {
            return Forbid();
        }

        employee.FullName = dto.FullName.Trim();
        employee.Department = dto.Department.Trim();
        employee.Phone = dto.Phone;
        employee.Address = dto.Address;

        if (dto.RemoveProfileImage)
        {
            employee.ProfileImageUrl = null;
        }
        else if (!string.IsNullOrWhiteSpace(dto.ProfileImageBase64))
        {
            var savedUrl = await _profileImages.SaveBase64Async(dto.ProfileImageBase64);
            if (savedUrl is not null)
            {
                employee.ProfileImageUrl = savedUrl;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            employee.EmployeeId,
            employee.FullName,
            employee.Department,
            employee.Phone,
            employee.Address,
            employee.ProfileImageUrl
        });
    }

    // PUT: api/employees/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateEmployee(int id, [FromBody] EmployeeUpdateDto dto)
    {
        var employee = await _context.Employees.FindAsync(id);
        if (employee == null) return NotFound();

        employee.EmployeeCode = dto.EmployeeCode;
        employee.FullName = dto.FullName;
        employee.Department = dto.Department;
        employee.Salary = dto.Salary;
        employee.Role = dto.Role;
        employee.Phone = dto.Phone;
        employee.Address = dto.Address;
        employee.ProfileImageUrl = dto.ProfileImageUrl;
        employee.AnnualLeaveQuota = dto.AnnualLeaveQuota;

        // Keep remaining leaves in sync when annual quota changes (simple rule)
        if (employee.RemainingLeaves > employee.AnnualLeaveQuota)
        {
            employee.RemainingLeaves = employee.AnnualLeaveQuota;
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/employees/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteEmployee(int id)
    {
        var employee = await _context.Employees.FindAsync(id);
        if (employee == null) return NotFound();

        _context.Employees.Remove(employee);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}


