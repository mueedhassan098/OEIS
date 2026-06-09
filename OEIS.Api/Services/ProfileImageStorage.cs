using OEIS.Application.Services;

namespace OEIS.Api.Services;

public class ProfileImageStorage : IProfileImageStorage
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ProfileImageStorage> _logger;

    public ProfileImageStorage(IWebHostEnvironment environment, ILogger<ProfileImageStorage> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<string?> SaveBase64Async(string? base64Data, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(base64Data))
        {
            return null;
        }

        try
        {
            var payload = base64Data.Trim();
            string extension = ".jpg";

            var commaIndex = payload.IndexOf(',');
            if (commaIndex >= 0)
            {
                var header = payload[..commaIndex];
                payload = payload[(commaIndex + 1)..];
                extension = header switch
                {
                    var h when h.Contains("image/png", StringComparison.OrdinalIgnoreCase) => ".png",
                    var h when h.Contains("image/webp", StringComparison.OrdinalIgnoreCase) => ".webp",
                    _ => ".jpg"
                };
            }

            if (!AllowedExtensions.Contains(extension))
            {
                extension = ".jpg";
            }

            var bytes = Convert.FromBase64String(payload);
            if (bytes.Length == 0 || bytes.Length > MaxBytes)
            {
                return null;
            }

            var webRoot = _environment.WebRootPath
                ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            var profilesDir = Path.Combine(webRoot, "uploads", "profiles");
            Directory.CreateDirectory(profilesDir);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(profilesDir, fileName);
            await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);

            return $"/uploads/profiles/{fileName}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save profile image.");
            return null;
        }
    }
}
