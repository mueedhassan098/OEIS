using FluentValidation;
using OEIS.Application.Common;
using OEIS.Domain.Entities;

namespace OEIS.Application.DTOs;

// Employee DTOs
public record EmployeeCreateDto(
    string FullName,
    string Email,
    string Password,
    string Department,
    decimal Salary,
    string Role)
{
    public string? EmployeeCode { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string? ProfileImageUrl { get; init; }
    public int AnnualLeaveQuota { get; init; }
}

public record EmployeeUpdateDto(
    string FullName,
    string Department,
    decimal Salary,
    string Role)
{
    public string? EmployeeCode { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string? ProfileImageUrl { get; init; }
    public int AnnualLeaveQuota { get; init; }
}

public record EmployeeRegistrationDto(
    string FullName,
    string Email,
    string Password,
    string Department)
{
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string? ProfileImageBase64 { get; init; }
}

public record EmployeeRegistrationDecisionDto(
    string? Note);

public record EmployeeSelfProfileUpdateDto(
    string FullName,
    string Department)
{
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string? ProfileImageBase64 { get; init; }
    public bool RemoveProfileImage { get; init; }
}

// Attendance DTOs
public record AttendanceCheckInDto(int EmployeeId, DateTime EntryTime);
public record AttendanceCheckOutDto(int AttendanceId, DateTime ExitTime);

// Admin attendance reporting DTOs
public record AdminAttendanceRowDto(
    int EmployeeId,
    string FullName,
    string Department,
    DateOnly Date,
    DateTime? EntryTime,
    DateTime? ExitTime);

// Leave DTOs
public record LeaveCreateDto(
    int EmployeeId,
    string LeaveType,
    DateTime FromDate,
    DateTime ToDate);

public record AdminLeaveRowDto(
    int LeaveId,
    int EmployeeId,
    string FullName,
    string Department,
    string LeaveType,
    DateOnly FromDate,
    DateOnly ToDate,
    string Status);

// Salary DTOs
public record SalaryGenerateDto(int EmployeeId, string Month);

public record DashboardSummaryDto(
    int TotalEmployees,
    int ActiveLeaves,
    int TodayPresent,
    int TodayAbsent);

public record DepartmentStatsDto(
    string Department,
    int EmployeeCount,
    decimal TotalSalary);

public record PayrollRunRequestDto(string Month);

public record PayrollRunResultDto(
    string Month,
    int EmployeeCount,
    decimal TotalNetAmount);

// Auth DTOs
public record LoginDto(string Email, string Password);
public record RegisterDto(
    string FullName,
    string Email,
    string Password,
    string Department,
    decimal Salary,
    string Role);

// API-specific auth DTOs used by AuthController
public record LoginRequestDto(string Email, string Password);

public record LoginResponseDto(
    int EmployeeId,
    string FullName,
    string Email,
    string Role,
    string Token,
    string? ProfileImageUrl = null);

public record ForgotPasswordRequestDto(string Email);

public record ForgotPasswordResponseDto(string Message, string? ResetLink);

public record ResetPasswordRequestDto(string Email, string Token, string NewPassword);

public record MessageResponseDto(string Message);

public record VerifyGoogleEmailRequestDto(string Email);

public record VerifyGoogleEmailResponseDto(bool IsGoogleEmail, string Message);

// Validators
public class EmployeeCreateDtoValidator : AbstractValidator<EmployeeCreateDto>
{
    public EmployeeCreateDtoValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
        RuleFor(x => x.Department).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Salary).GreaterThan(0);
        RuleFor(x => x.Role).NotEmpty().Must(r => r is "Admin" or "Employee");
    }
}

public class EmployeeUpdateDtoValidator : AbstractValidator<EmployeeUpdateDto>
{
    public EmployeeUpdateDtoValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Department).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Salary).GreaterThan(0);
        RuleFor(x => x.Role).NotEmpty().Must(r => r is "Admin" or "Employee");
    }
}

public class EmployeeRegistrationDtoValidator : AbstractValidator<EmployeeRegistrationDto>
{
    public EmployeeRegistrationDtoValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
        RuleFor(x => x.Department).NotEmpty().MaximumLength(50);
    }
}

public class AttendanceCheckInDtoValidator : AbstractValidator<AttendanceCheckInDto>
{
    public AttendanceCheckInDtoValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.EntryTime).LessThanOrEqualTo(AppTime.Now.AddMinutes(5));
    }
}

public class AttendanceCheckOutDtoValidator : AbstractValidator<AttendanceCheckOutDto>
{
    public AttendanceCheckOutDtoValidator()
    {
        RuleFor(x => x.AttendanceId).GreaterThan(0);
        RuleFor(x => x.ExitTime).LessThanOrEqualTo(AppTime.Now.AddMinutes(5));
    }
}

public class LeaveCreateDtoValidator : AbstractValidator<LeaveCreateDto>
{
    public LeaveCreateDtoValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.LeaveType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.FromDate)
            .LessThanOrEqualTo(x => x.ToDate)
            .WithMessage("FromDate must be before or equal to ToDate");
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate);
    }
}

public class SalaryGenerateDtoValidator : AbstractValidator<SalaryGenerateDto>
{
    public SalaryGenerateDtoValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.Month).NotEmpty().MaximumLength(20);
    }
}

public class LoginDtoValidator : AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class ForgotPasswordRequestDtoValidator : AbstractValidator<ForgotPasswordRequestDto>
{
    public ForgotPasswordRequestDtoValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public class ResetPasswordRequestDtoValidator : AbstractValidator<ResetPasswordRequestDto>
{
    public ResetPasswordRequestDtoValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6);
    }
}

public class RegisterDtoValidator : AbstractValidator<RegisterDto>
{
    public RegisterDtoValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
        RuleFor(x => x.Department).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Salary).GreaterThan(0);
        RuleFor(x => x.Role).NotEmpty().Must(r => r is "Admin" or "Employee");
    }
}
