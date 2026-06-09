namespace OEIS.Blazor.Services;

public static class ProfileImageHelper
{
    public static string? ToAbsoluteUrl(HttpClient http, string? profileImageUrl)
    {
        if (string.IsNullOrWhiteSpace(profileImageUrl))
        {
            return null;
        }

        if (profileImageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            profileImageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            profileImageUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return profileImageUrl;
        }

        var baseUri = http.BaseAddress ?? new Uri("https://localhost:7094/");
        return new Uri(baseUri, profileImageUrl.TrimStart('/')).ToString();
    }

    public static string GetInitials(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return "?";
        }

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
        }

        return parts[0].Length >= 2
            ? parts[0][..2].ToUpperInvariant()
            : parts[0].ToUpperInvariant();
    }
}
