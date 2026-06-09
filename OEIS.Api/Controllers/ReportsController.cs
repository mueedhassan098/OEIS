using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OEIS.Infrastructure.Data;

namespace OEIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class ReportsController : ControllerBase
{
    private readonly OeisDbContext _context;

    public ReportsController(OeisDbContext context)
    {
        _context = context;
    }

    // GET: api/reports/attendance?from=2026-03-01&to=2026-03-31
    [HttpGet("attendance")]
    public async Task<IActionResult> GetAttendanceCsv([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        var query = from <= to
            ? _context.Attendances
                .Include(a => a.Employee)
                .Where(a => a.Date >= from && a.Date <= to)
            : _context.Attendances
                .Include(a => a.Employee)
                .Where(a => a.Date == from);

        var records = await query
            .OrderBy(a => a.Date)
            .ThenBy(a => a.EmployeeId)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Date,EmployeeId,EmployeeName,Department,EntryTime,ExitTime");

        foreach (var a in records)
        {
            var name = a.Employee?.FullName ?? string.Empty;
            var dept = a.Employee?.Department ?? string.Empty;
            var entry = a.EntryTime.ToString("O");
            var exit = a.ExitTime?.ToString("O") ?? string.Empty;

            sb.AppendLine($"{a.Date:yyyy-MM-dd},{a.EmployeeId},\"{name}\",\"{dept}\",{entry},{exit}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"attendance_{from:yyyyMMdd}_{to:yyyyMMdd}.csv";
        return File(bytes, "text/csv", fileName);
    }

    // GET: api/reports/salaries?month=2026-03
    [HttpGet("salaries")]
    public async Task<IActionResult> GetSalariesCsv([FromQuery] string month)
    {
        if (string.IsNullOrWhiteSpace(month))
        {
            return BadRequest("Month is required, e.g. 2026-03.");
        }

        var records = await _context.Salaries
            .Include(s => s.Employee)
            .Where(s => s.Month == month)
            .OrderBy(s => s.EmployeeId)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("EmployeeId,EmployeeName,Department,Month,Amount,GeneratedOn");

        foreach (var s in records)
        {
            var name = s.Employee?.FullName ?? string.Empty;
            var dept = s.Employee?.Department ?? string.Empty;
            var generated = s.GeneratedOn.ToString("O");

            sb.AppendLine($"{s.EmployeeId},\"{name}\",\"{dept}\",{s.Month},{s.Amount},{generated}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"salaries_{month}.csv";
        return File(bytes, "text/csv", fileName);
    }
}

