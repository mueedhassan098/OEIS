namespace OEIS.Blazor.Services;

public class AuthState
{
    public bool IsAuthenticated => CurrentUser is not null;

    public UserInfo? CurrentUser { get; private set; }
    public string? JwtToken { get; private set; }

    public event Action? OnChange;

    public void SetUser(UserInfo? user, string? jwtToken = null)
    {
        CurrentUser = user;
        JwtToken = jwtToken;
        OnChange?.Invoke();
    }
}

public class UserInfo
{
    public int EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
}

