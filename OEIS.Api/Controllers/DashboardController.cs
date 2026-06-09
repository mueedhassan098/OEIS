using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OEIS.Application.Common;
using OEIS.Application.DTOs;
using OEIS.Infrastructure.Data;

namespace OEIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class DashboardController : ControllerBase
{
    private readonly OeisDbContext _context;

    public DashboardController(OeisDbContext context)
    {
        _context = context;
    }

    // GET: api/dashboard/summary
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary()
    {
        var today = AppTime.Today;

        var totalEmployees = await _context.Employees.CountAsync();

        var activeLeaves = await _context.Leaves
            .CountAsync(l =>
                l.Status == "Approved" &&
                l.FromDate <= today &&
                l.ToDate >= today);

        var todayPresent = await _context.Attendances
            .Where(a => a.Date == today)
            .Select(a => a.EmployeeId)
            .Distinct()
            .CountAsync();

        var todayAbsent = Math.Max(0, totalEmployees - todayPresent);

        var dto = new DashboardSummaryDto(
            totalEmployees,
            activeLeaves,
            todayPresent,
            todayAbsent);

        return dto;
    }

    // GET: api/dashboard/department-stats
    [HttpGet("department-stats")]
    public async Task<ActionResult<IEnumerable<DepartmentStatsDto>>> GetDepartmentStats()
    {
        var stats = await _context.Employees
            .AsNoTracking()
            .Select(e => new
            {
                Department = string.IsNullOrWhiteSpace(e.Department) ? "Unassigned" : e.Department,
                e.Salary
            })
            .GroupBy(e => e.Department)
            .Select(g => new DepartmentStatsDto(
                g.Key,
                g.Count(),
                g.Sum(e => e.Salary)))
            .OrderBy(s => s.Department)
            .ToListAsync();

        return stats;
    }
}

