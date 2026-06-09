using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OEIS.Application.DTOs;
using OEIS.Domain.Entities;
using OEIS.Infrastructure.Data;

namespace OEIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalariesController : ControllerBase
{
    private readonly OeisDbContext _context;

    public SalariesController(OeisDbContext context)
    {
        _context = context;
    }

    // POST: api/salaries/generate
    [HttpPost("generate")]
    public async Task<ActionResult<Salary>> Generate([FromBody] SalaryGenerateDto dto)
    {
        var employee = await _context.Employees.FindAsync(dto.EmployeeId);
        if (employee == null) return NotFound("Employee not found.");

        var exists = await _context.Salaries
            .AnyAsync(s => s.EmployeeId == dto.EmployeeId && s.Month == dto.Month);
        if (exists) return Conflict("Salary already generated for this month.");

        var salary = new Salary
        {
            EmployeeId = dto.EmployeeId,
            Month = dto.Month,
            Amount = employee.Salary,
            GeneratedOn = DateTime.UtcNow
        };
        _context.Salaries.Add(salary);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetByEmployee), new { employeeId = dto.EmployeeId }, salary);
    }

    // GET: api/salaries/employee/1
    [HttpGet("employee/{employeeId:int}")]
    public async Task<ActionResult<IEnumerable<Salary>>> GetByEmployee(int employeeId)
    {
        var list = await _context.Salaries
            .Where(s => s.EmployeeId == employeeId && s.IsLocked)
            .AsNoTracking()
            .ToListAsync();
        return list;
    }
}


