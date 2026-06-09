using System.Net.Mail;
using DnsClient;
using Microsoft.Extensions.Configuration;
using OEIS.Application.Services;

namespace OEIS.Infrastructure.Services;

public class GoogleEmailVerificationService : IGoogleEmailVerificationService
{
    private static readonly HashSet<string> GoogleConsumerDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com",
        "googlemail.com"
    };

    private readonly IConfiguration _configuration;
    private readonly LookupClient _lookupClient;

    public GoogleEmailVerificationService(IConfiguration configuration)
    {
        _configuration = configuration;
        _lookupClient = new LookupClient();
    }

    public async Task<GoogleEmailCheckResult> VerifyAsync(string email, CancellationToken cancellationToken = default)
    {
        if (!_configuration.GetValue("GoogleEmail:RequireForEmployeeRegistration", true))
        {
            return new GoogleEmailCheckResult(true, "Google email check is disabled.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return new GoogleEmailCheckResult(false, "Email is required.");
        }

        if (!TryGetDomain(email, out var domain))
        {
            return new GoogleEmailCheckResult(false, "Enter a valid email address.");
        }

        if (GoogleConsumerDomains.Contains(domain))
        {
            return new GoogleEmailCheckResult(
                true,
                "This Gmail address can be used for registration.");
        }

        try
        {
            var mxResult = await _lookupClient.QueryAsync(domain, QueryType.MX, cancellationToken: cancellationToken);
            var mxHosts = mxResult.Answers
                .OfType<DnsClient.Protocol.MxRecord>()
                .Select(r => r.Exchange.Value.TrimEnd('.'))
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .ToList();

            if (mxHosts.Count == 0)
            {
                return new GoogleEmailCheckResult(
                    false,
                    "This email is not registered with Google. Use a Gmail or Google Workspace address.");
            }

            var usesGoogleMail = mxHosts.Any(IsGoogleMailHost);
            if (usesGoogleMail)
            {
                return new GoogleEmailCheckResult(
                    true,
                    "This Google Workspace email can be used for registration.");
            }

            return new GoogleEmailCheckResult(
                false,
                "This email is not registered with Google. Use a Gmail or Google Workspace address.");
        }
        catch (Exception)
        {
            return new GoogleEmailCheckResult(
                false,
                "Could not verify the email domain. Check your internet connection and try again.");
        }
    }

    private static bool TryGetDomain(string email, out string domain)
    {
        domain = string.Empty;
        try
        {
            var parsed = new MailAddress(email.Trim());
            domain = parsed.Host.ToLowerInvariant();
            return !string.IsNullOrWhiteSpace(domain);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsGoogleMailHost(string host)
    {
        host = host.TrimEnd('.').ToLowerInvariant();
        return host.EndsWith(".google.com", StringComparison.Ordinal) ||
               host.Equals("google.com", StringComparison.Ordinal) ||
               host.Contains("googlemail.com", StringComparison.Ordinal);
    }
}
