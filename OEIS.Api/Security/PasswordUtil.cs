using Microsoft.AspNetCore.Identity;

namespace OEIS.Api.Security;

public static class PasswordUtil
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object PasswordHasherUser = new();

    public static string Hash(string password) => Hasher.HashPassword(new object(), password);

    public static bool Verify(string storedValue, string suppliedPassword)
    {
        if (string.IsNullOrWhiteSpace(storedValue)) return false;

        try
        {
            var verify = Hasher.VerifyHashedPassword(PasswordHasherUser, storedValue, suppliedPassword);
            if (verify is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded)
                return true;
        }
        catch (FormatException)
        {
            // Legacy rows may still store plain-text values; fallback check below handles them.
        }
        catch (ArgumentException)
        {
            // Treat malformed hashes as non-hash legacy values and use fallback check.
        }

        // Backward compatibility for legacy plain-text rows
        return string.Equals(storedValue, suppliedPassword, StringComparison.Ordinal);
    }
}
