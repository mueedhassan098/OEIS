namespace OEIS.Application.Services;

public interface IGoogleEmailVerificationService
{
    /// <summary>
    /// Returns true when the address is a Gmail account or its domain uses Google mail (MX) servers.
    /// </summary>
    Task<GoogleEmailCheckResult> VerifyAsync(string email, CancellationToken cancellationToken = default);
}

public record GoogleEmailCheckResult(bool IsGoogleEmail, string Message);
