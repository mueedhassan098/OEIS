using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OEIS.Application.DTOs;
using OEIS.Application.Services;
using OEIS.Domain.Entities;
using OEIS.Infrastructure.Data;

namespace OEIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LeavesController : ControllerBase
{
    private readonly OeisDbContext _context;
    private readonly INotificationService _notifications;
    private readonly IPortalActivityService _portal;

    public LeavesController(OeisDbContext context, INotificationService notifications, IPortalActivityService portal)
    {
        _context = context;
        _notifications = notifications;
        _portal = portal;
    }

    // POST: api/leaves
    [HttpPost]
    public async Task<IActionResult> Apply([FromBody] LeaveCreateDto dto)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == dto.EmployeeId);
        if (employee is null) return NotFound("Employee not found.");

        if (dto.ToDate.Date < dto.FromDate.Date)
        {
            return BadRequest("To date cannot be before from date.");
        }

        // Simple leave-days calculation (inclusive)
        var from = DateOnly.FromDateTime(dto.FromDate.Date);
        var to = DateOnly.FromDateTime(dto.ToDate.Date);
        var daysRequested = (to.DayNumber - from.DayNumber) + 1;
        if (daysRequested <= 0)
        {
            return BadRequest("Invalid leave range.");
        }

        var leave = new Leave
        {
            EmployeeId = dto.EmployeeId,
            LeaveType = dto.LeaveType,
            FromDate = from,
            ToDate = to,
            Status = "Pending"
        };

        _context.Leaves.Add(leave);
        await _context.SaveChangesAsync();

        // Return a simple shape to avoid serialization cycles
        var result = new
        {
            leave.LeaveId,
            leave.EmployeeId,
            leave.LeaveType,
            leave.FromDate,
            leave.ToDate,
            leave.Status
        };

        return CreatedAtAction(nameof(GetByEmployee), new { employeeId = dto.EmployeeId }, result);
    }

    // GET: api/leaves/employee/1
    [HttpGet("employee/{employeeId:int}")]
    public async Task<ActionResult<IEnumerable<Leave>>> GetByEmployee(int employeeId)
    {
        var list = await _context.Leaves
            .Where(l => l.EmployeeId == employeeId)
            .AsNoTracking()
            .ToListAsync();
        return list;
    }

    // PUT: api/leaves/{id}/approve
    [HttpPut("{id:int}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Approve(int id)
    {
        var leave = await _context.Leaves.FindAsync(id);
        if (leave == null) return NotFound();

        if (leave.Status == "Approved")
        {
            return NoContent();
        }

        var employee = await _context.Employees.FindAsync(leave.EmployeeId);
        if (employee is null) return NotFound("Employee not found for leave.");

        var days = (leave.ToDate.DayNumber - leave.FromDate.DayNumber) + 1;
        if (days <= 0)
        {
            return BadRequest("Invalid leave range.");
        }

        // Simple rule: deduct days (can go down to zero, never negative)
        var newRemaining = employee.RemainingLeaves - days;
        employee.RemainingLeaves = newRemaining < 0 ? 0 : newRemaining;
        leave.Status = "Approved";
        await _context.SaveChangesAsync();

        await _notifications.SendLeaveStatusAsync(employee.Email, employee.Phone, leave.LeaveType, leave.Status);

        await _portal.RecordAsync(
            "Leave approved",
            $"{employee.FullName} — {leave.LeaveType} ({leave.FromDate:MMM d}–{leave.ToDate:MMM d}).",
            "Success",
            "/home");

        return NoContent();
    }

    // PUT: api/leaves/{id}/reject
    [HttpPut("{id:int}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reject(int id)
    {
        var leave = await _context.Leaves.FindAsync(id);
        if (leave == null) return NotFound();

        var employee = await _context.Employees.FindAsync(leave.EmployeeId);
        if (employee != null && leave.Status == "Approved")
        {
            var days = (leave.ToDate.DayNumber - leave.FromDate.DayNumber) + 1;
            if (days > 0)
            {
                employee.RemainingLeaves += days;
            }
        }

        leave.Status = "Rejected";
        await _context.SaveChangesAsync();

        if (employee != null)
        {
            await _notifications.SendLeaveStatusAsync(employee.Email, employee.Phone, leave.LeaveType, leave.Status);
        }

        await _portal.RecordAsync(
            "Leave rejected",
            $"{(employee?.FullName ?? "Employee")} — {leave.LeaveType} was not approved.",
            "Warning",
            "/home");

        return NoContent();
    }

    // DELETE: api/leaves/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var leave = await _context.Leaves.FindAsync(id);
        if (leave == null) return NotFound();

        _context.Leaves.Remove(leave);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // GET: api/leaves/admin?status=Pending&department=IT
    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<AdminLeaveRowDto>>> GetAdminLeaves([FromQuery] string? status, [FromQuery] string? department)
    {
        var query = _context.Leaves
            .Include(l => l.Employee)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(l => l.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            query = query.Where(l => l.Employee != null && l.Employee.Department == department);
        }

        var rows = await query
            .OrderBy(l => l.Status)
            .ThenBy(l => l.FromDate)
            .Select(l => new AdminLeaveRowDto(
                l.LeaveId,
                l.EmployeeId,
                l.Employee != null ? l.Employee.FullName : string.Empty,
                l.Employee != null ? l.Employee.Department : string.Empty,
                l.LeaveType,
                l.FromDate,
                l.ToDate,
                l.Status))
            .ToListAsync();

        return rows;
    }
}


