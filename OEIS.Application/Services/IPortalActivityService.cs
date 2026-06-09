namespace OEIS.Application.Services;

/// <summary>
/// Records portal-wide notifications visible to all authenticated users.
/// </summary>
public interface IPortalActivityService
{
    Task RecordAsync(string title, string message, string type = "Info", string? link = null, CancellationToken cancellationToken = default);
}
