using OEIS.Application.Services;

namespace OEIS.Infrastructure.Services;

public class ConsoleNotificationService : INotificationService
{
    public Task SendCredentialsAsync(string? email, string? phone, string password)
    {
        Console.WriteLine($"[Notification] Sending credentials to {email ?? phone}: password={password}");
        return Task.CompletedTask;
    }

    public Task SendLeaveStatusAsync(string? email, string? phone, string leaveType, string status)
    {
        Console.WriteLine($"[Notification] Leave '{leaveType}' status changed to {status} for {email ?? phone}");
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string? email, string? phone, string resetLink)
    {
        Console.WriteLine($"[Notification] Password reset link for {email ?? phone}: {resetLink}");
        return Task.CompletedTask;
    }
}

