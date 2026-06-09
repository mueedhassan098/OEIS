using System.Text.Json.Serialization;

namespace OEIS.Domain.Entities;

public class Employee
{
    public int EmployeeId { get; set; }
    public string? EmployeeCode { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiresUtc { get; set; }
    public string Department { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public string Role { get; set; } = "Employee"; // Admin / Employee
    public string RegistrationStatus { get; set; } = "Approved"; // Pending / Approved / Rejected
    public string? RegistrationReviewNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? ProfileImageUrl { get; set; }

    // Simple leave accounting for this project
    public int AnnualLeaveQuota { get; set; }
    public int RemainingLeaves { get; set; }

    public int? PayScaleId { get; set; }
    [JsonIgnore]
    public PayScale? PayScale { get; set; }

    [JsonIgnore]
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    [JsonIgnore]
    public ICollection<Leave> Leaves { get; set; } = new List<Leave>();
    [JsonIgnore]
    public ICollection<Salary> Salaries { get; set; } = new List<Salary>();
}

public class Attendance
{
    public int AttendanceId { get; set; }
    public int EmployeeId { get; set; }
    [JsonIgnore]
    public Employee? Employee { get; set; }
    public DateTime EntryTime { get; set; }
    public DateTime? ExitTime { get; set; }
    public DateOnly Date { get; set; }
}

public class Leave
{
    public int LeaveId { get; set; }
    public int EmployeeId { get; set; }
    [JsonIgnore]
    public Employee? Employee { get; set; }
    public string LeaveType { get; set; } = string.Empty; // Casual / Sick etc.
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string Status { get; set; } = "Pending"; // Pending / Approved / Rejected
}

public class Salary
{
    public int SalaryId { get; set; }
    public int EmployeeId { get; set; }
    [JsonIgnore]
    public Employee? Employee { get; set; }
    public string Month { get; set; } = string.Empty; // e.g. "2026-03"
    public decimal Amount { get; set; }
    public decimal BasicAmount { get; set; }
    public decimal OvertimeAmount { get; set; }
    public decimal Bonus { get; set; }
    public decimal Deductions { get; set; }
    public bool IsLocked { get; set; }
    public DateTime GeneratedOn { get; set; } = DateTime.UtcNow;
}

public class PayScale
{
    public int PayScaleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal BaseSalary { get; set; }
    public int AnnualLeaveQuota { get; set; }

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}

/// <summary>
/// Company-wide activity shown in the notification bell for all signed-in users.
/// </summary>
public class PortalActivity
{
    public long PortalActivityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    /// <summary>Info, Success, or Warning</summary>
    public string Type { get; set; } = "Info";
    public string? Link { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
