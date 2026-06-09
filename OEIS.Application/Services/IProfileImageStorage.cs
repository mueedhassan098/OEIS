namespace OEIS.Application.Services;

public interface IProfileImageStorage
{
    /// <summary>
    /// Saves a base64-encoded image and returns a relative URL (e.g. /uploads/profiles/abc.jpg).
    /// </summary>
    Task<string?> SaveBase64Async(string? base64Data, CancellationToken cancellationToken = default);
}
