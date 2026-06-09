using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OEIS.Infrastructure.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Security.Claims;

namespace OEIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PayslipsController : ControllerBase
{
    private readonly OeisDbContext _context;

    public PayslipsController(OeisDbContext context)
    {
        _context = context;
    }

    // GET: api/payslips/123
    [HttpGet("{salaryId:int}")]
    public async Task<IActionResult> GetPayslip(int salaryId)
    {
        var salary = await _context.Salaries
            .Include(s => s.Employee)
            .FirstOrDefaultAsync(s => s.SalaryId == salaryId);

        if (salary is null) return NotFound();
        if (!salary.IsLocked) return BadRequest("Payslip is only available for finalized salaries.");

        var employee = salary.Employee;
        if (employee is null) return NotFound("Employee not found.");

        var role = User.FindFirstValue(ClaimTypes.Role);
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(userIdClaim, out var callerId) || callerId != employee.EmployeeId)
            {
                return Forbid();
            }
        }

        var bytes = GeneratePayslipPdf(employee.FullName, employee.Department, salary.Month,
            salary.BasicAmount, salary.OvertimeAmount, salary.Bonus, salary.Deductions, salary.Amount);

        var fileName = $"Payslip_{salary.Month}_{employee.EmployeeId}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    private static byte[] GeneratePayslipPdf(
        string fullName,
        string department,
        string month,
        decimal basic,
        decimal overtime,
        decimal bonus,
        decimal deductions,
        decimal net)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.A4);

                page.Header().Text("Payslip").FontSize(20).Bold().AlignCenter();

                page.Content().Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Text($"Month: {month}");
                    col.Item().Text($"Employee: {fullName}");
                    col.Item().Text($"Department: {department}");

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        void Row(string label, string value)
                        {
                            table.Cell().Element(CellStyle).Text(label);
                            table.Cell().Element(CellStyle).Text(value);
                        }

                        static IContainer CellStyle(IContainer container) =>
                            container.BorderBottom(0.5f).PaddingVertical(4);

                        Row("Basic", basic.ToString("C"));
                        Row("Overtime", overtime.ToString("C"));
                        Row("Bonus", bonus.ToString("C"));
                        Row("Deductions", deductions.ToString("C"));
                        Row("Net pay", net.ToString("C"));
                    });
                });
            });
        });

        return document.GeneratePdf();
    }
}

