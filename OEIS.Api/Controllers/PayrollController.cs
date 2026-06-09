using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OEIS.Application.DTOs;
using OEIS.Application.Services;
using OEIS.Infrastructure.Data;

namespace OEIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class PayrollController : ControllerBase
{
    private readonly OeisDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IPortalActivityService _portal;

    public PayrollController(OeisDbContext context, IConfiguration configuration, IPortalActivityService portal)
    {
        _context = context;
        _configuration = configuration;
        _portal = portal;
    }

    // POST: api/payroll/run
    [HttpPost("run")]
    public async Task<ActionResult<PayrollRunResultDto>> Run([FromBody] PayrollRunRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Month))
        {
            return BadRequest("Month is required, e.g. 2026-03.");
        }

        if (!DateTime.TryParse(request.Month + "-01", out var monthDate))
        {
            return BadRequest("Month must be in format YYYY-MM.");
        }

        var year = monthDate.Year;
        var month = monthDate.Month;

        var standardHoursPerDay = _configuration.GetValue<decimal?>("Payroll:StandardHoursPerDay") ?? 8m;
        var overtimeRatePerHour = _configuration.GetValue<decimal?>("Payroll:OvertimeRatePerHour") ?? 0m;

        var employees = await _context.Employees.AsNoTracking().ToListAsync();
        if (employees.Count == 0)
        {
            return Ok(new PayrollRunResultDto(request.Month, 0, 0m));
        }

        var employeeIds = employees.Select(e => e.EmployeeId).ToList();
        var attendances = await _context.Attendances
            .Where(a => employeeIds.Contains(a.EmployeeId)
                        && a.Date.Year == year
                        && a.Date.Month == month)
            .ToListAsync();

        var salaries = await _context.Salaries
            .Where(s => s.Month == request.Month)
            .ToListAsync();

        var salaryLookup = salaries.ToDictionary(s => s.EmployeeId, s => s);

        decimal totalNet = 0m;

        foreach (var employee in employees)
        {
            var employeeAttendance = attendances.Where(a => a.EmployeeId == employee.EmployeeId).ToList();

            decimal overtimeHours = 0m;
            foreach (var a in employeeAttendance)
            {
                if (a.ExitTime is null) continue;
                var hours = (decimal)(a.ExitTime.Value - a.EntryTime).TotalHours;
                if (hours > standardHoursPerDay)
                {
                    overtimeHours += hours - standardHoursPerDay;
                }
            }

            var overtimeAmount = overtimeHours * overtimeRatePerHour;
            var basic = employee.Salary;
            var bonus = 0m;
            var deductions = 0m;
            var net = basic + overtimeAmount + bonus - deductions;

            if (!salaryLookup.TryGetValue(employee.EmployeeId, out var salary))
            {
                salary = new OEIS.Domain.Entities.Salary
                {
                    EmployeeId = employee.EmployeeId,
                    Month = request.Month
                };
                _context.Salaries.Add(salary);
                salaryLookup[employee.EmployeeId] = salary;
            }

            if (salary.IsLocked)
            {
                totalNet += salary.Amount;
                continue;
            }

            salary.BasicAmount = basic;
            salary.OvertimeAmount = overtimeAmount;
            salary.Bonus = bonus;
            salary.Deductions = deductions;
            salary.Amount = net;
            salary.GeneratedOn = DateTime.UtcNow;

            totalNet += net;
        }

        await _context.SaveChangesAsync();

        var result = new PayrollRunResultDto(request.Month, employees.Count, totalNet);
        return Ok(result);
    }

    // POST: api/payroll/finalize?month=2026-03
    [HttpPost("finalize")]
    public async Task<IActionResult> Finalize([FromQuery] string month)
    {
        if (string.IsNullOrWhiteSpace(month))
        {
            return BadRequest("Month is required.");
        }

        var salaries = await _context.Salaries
            .Where(s => s.Month == month)
            .ToListAsync();

        if (salaries.Count == 0)
        {
            return NotFound("No salaries found for this month.");
        }

        foreach (var s in salaries)
        {
            s.IsLocked = true;
        }

        await _context.SaveChangesAsync();

        await _portal.RecordAsync(
            "Salaries released",
            $"Payroll finalized for {month}. Payslips are available under My Salaries.",
            "Success",
            "/home");

        return NoContent();
    }
}

