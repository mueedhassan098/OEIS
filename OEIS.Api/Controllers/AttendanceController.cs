using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OEIS.Application.Common;
using OEIS.Application.DTOs;
using OEIS.Application.Services;
using OEIS.Domain.Entities;
using OEIS.Infrastructure.Data;

namespace OEIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly OeisDbContext _context;
    private readonly IPortalActivityService _portal;

    public AttendanceController(OeisDbContext context, IPortalActivityService portal)
    {
        _context = context;
        _portal = portal;
    }

    // POST: api/attendance/check-in
    [HttpPost("check-in")]
    public async Task<ActionResult<Attendance>> CheckIn([FromBody] AttendanceCheckInDto dto)
    {
        var employee = await _context.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeId == dto.EmployeeId);
        if (employee is null) return NotFound("Employee not found.");

        var today = DateOnly.FromDateTime(dto.EntryTime.Date);
        var existing = await _context.Attendances
            .FirstOrDefaultAsync(a => a.EmployeeId == dto.EmployeeId && a.Date == today);
        if (existing != null) return Conflict("Attendance already marked for today.");

        var attendance = new Attendance
        {
            EmployeeId = dto.EmployeeId,
            EntryTime = dto.EntryTime,
            Date = today
        };
        _context.Attendances.Add(attendance);
        await _context.SaveChangesAsync();

        await _portal.RecordAsync(
            "Attendance marked",
            $"{employee.FullName} ({employee.Department}) checked in at {dto.EntryTime:HH:mm} — {today:yyyy-MM-dd}.",
            "Info",
            "/home");

        return CreatedAtAction(nameof(GetMonthly), new { employeeId = dto.EmployeeId, month = today.Month, year = today.Year }, attendance);
    }

    // POST: api/attendance/check-out
    [HttpPost("check-out")]
    public async Task<IActionResult> CheckOut([FromBody] AttendanceCheckOutDto dto)
    {
        var attendance = await _context.Attendances.FindAsync(dto.AttendanceId);
        if (attendance == null) return NotFound();

        attendance.ExitTime = dto.ExitTime;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // GET: api/attendance/monthly?employeeId=1&month=3&year=2026
    [HttpGet("monthly")]
    public async Task<ActionResult<IEnumerable<Attendance>>> GetMonthly(int employeeId, int month, int year)
    {
        var list = await _context.Attendances
            .Where(a => a.EmployeeId == employeeId &&
                        a.Date.Month == month &&
                        a.Date.Year == year)
            .AsNoTracking()
            .ToListAsync();

        return list;
    }

    // GET: api/attendance/admin/daily?date=2026-03-13&department=IT
    [HttpGet("admin/daily")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<AdminAttendanceRowDto>>> GetDaily([FromQuery] DateOnly date, [FromQuery] string? department)
    {
        var employeesQuery = _context.Employees.AsQueryable();

        if (!string.IsNullOrWhiteSpace(department))
        {
            employeesQuery = employeesQuery.Where(e => e.Department == department);
        }

        var rows = await employeesQuery
            .GroupJoin(
                _context.Attendances.Where(a => a.Date == date),
                e => e.EmployeeId,
                a => a.EmployeeId,
                (e, attendances) => new { e, attendances })
            .SelectMany(
                x => x.attendances.DefaultIfEmpty(),
                (x, a) => new AdminAttendanceRowDto(
                    x.e.EmployeeId,
                    x.e.FullName,
                    x.e.Department,
                    date,
                    a != null ? a.EntryTime : (DateTime?)null,
                    a != null ? a.ExitTime : (DateTime?)null))
            .OrderBy(r => r.FullName)
            .ToListAsync();

        return rows;
    }

    /// <summary>
    /// All attendance rows in a date range (any historical data). Optional department and search filter on server.
    /// </summary>
    [HttpGet("admin/history")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<AdminAttendanceRowDto>>> GetAdminHistory(
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] string? department,
        [FromQuery] string? search,
        [FromQuery] int maxRows = 25000)
    {
        maxRows = Math.Clamp(maxRows, 1, 100_000);

        var to = toDate ?? AppTime.Today;
        var from = fromDate ?? new DateOnly(2000, 1, 1);

        if (from > to)
            return BadRequest("fromDate must be on or before toDate.");

        var query = _context.Attendances
            .AsNoTracking()
            .Where(a => a.Date >= from && a.Date <= to)
            .Join(
                _context.Employees.AsNoTracking(),
                a => a.EmployeeId,
                e => e.EmployeeId,
                (a, e) => new { a, e });

        if (!string.IsNullOrWhiteSpace(department))
        {
            var d = department.Trim();
            query = query.Where(x => x.e.Department == d);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x =>
                x.e.FullName.Contains(s) ||
                x.e.Department.Contains(s) ||
                x.e.Email.Contains(s));
        }

        var list = await query
            .OrderByDescending(x => x.a.Date)
            .ThenBy(x => x.e.FullName)
            .Select(x => new AdminAttendanceRowDto(
                x.e.EmployeeId,
                x.e.FullName,
                x.e.Department,
                x.a.Date,
                x.a.EntryTime,
                x.a.ExitTime))
            .Take(maxRows)
            .ToListAsync();

        return list;
    }
}


