namespace OEIS.Application.Services;

public interface INotificationService
{
    Task SendCredentialsAsync(string? email, string? phone, string password);

    Task SendLeaveStatusAsync(string? email, string? phone, string leaveType, string status);

    Task SendPasswordResetAsync(string? email, string? phone, string resetLink);
}

