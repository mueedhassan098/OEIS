using System.Net.Http.Json;
using System.Text.Json.Serialization;
using OEIS.Application.Common;

namespace OEIS.Blazor.Services;

public sealed class NotificationCenterService : IDisposable
{
    private readonly HttpClient _http;
    private readonly AuthState _authState;
    private readonly HashSet<string> _readIds = new(StringComparer.Ordinal);
    private List<AppNotification> _items = new();

    public NotificationCenterService(HttpClient http, AuthState authState)
    {
        _http = http;
        _authState = authState;
        _authState.OnChange += HandleAuthChanged;
    }

    public IReadOnlyList<AppNotification> Items => _items;
    public int UnreadCount => _items.Count(x => !x.IsRead);

    public event Action? OnChange;

    public async Task RefreshAsync()
    {
        if (!_authState.IsAuthenticated || _authState.CurrentUser is null)
        {
            _items = new List<AppNotification>();
            _readIds.Clear();
            OnChange?.Invoke();
            return;
        }

        var user = _authState.CurrentUser;
        var portalFeed = await LoadPortalFeedNotificationsAsync();

        var roleSpecific = string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase)
            ? await BuildAdminNotifications()
            : await BuildEmployeeNotifications(user.EmployeeId);

        var notifications = portalFeed.Concat(roleSpecific).ToList();

        if (notifications.Count == 0)
        {
            notifications.Add(new AppNotification
            {
                Id = "feed-empty-hint",
                Title = "No alerts yet",
                Message = "Team activity yahan dikhegi. Agar yeh message hamesha aaye to Blazor app me HttpClient BaseAddress ko API server URL se match karwayein.",
                Type = NotificationType.Info,
                Link = "/home",
                CreatedAt = DateTime.Now
            });
        }

        foreach (var n in notifications)
            n.IsRead = _readIds.Contains(n.Id);

        _items = notifications
            .OrderByDescending(n => n.CreatedAt)
            .ToList();

        OnChange?.Invoke();
    }

    public void MarkRead(string id)
    {
        _readIds.Add(id);
        var item = _items.FirstOrDefault(x => x.Id == id);
        if (item is not null)
            item.IsRead = true;
        OnChange?.Invoke();
    }

    public void MarkAllRead()
    {
        foreach (var item in _items)
        {
            item.IsRead = true;
            _readIds.Add(item.Id);
        }
        OnChange?.Invoke();
    }

    private async Task<List<AppNotification>> BuildAdminNotifications()
    {
        var now = DateTime.Now;
        var list = new List<AppNotification>();

        try
        {
            var pending = await _http.GetFromJsonAsync<List<AdminLeaveVm>>("api/leaves/admin?status=Pending") ?? new List<AdminLeaveVm>();
            if (pending.Count > 0)
            {
                list.Add(new AppNotification
                {
                    Id = $"admin-pending-leaves-{pending.Count}",
                    Title = "Pending leave approvals",
                    Message = $"{pending.Count} leave request(s) need your approval.",
                    Type = NotificationType.Warning,
                    Link = "/leaves-admin",
                    CreatedAt = now.AddMinutes(-5)
                });
            }
        }
        catch
        {
            // keep feed resilient
        }

        try
        {
            var date = AppTime.Today.ToString("yyyy-MM-dd");
            var attendance = await _http.GetFromJsonAsync<List<AdminAttendanceVm>>($"api/attendance/admin/daily?date={date}") ?? new List<AdminAttendanceVm>();
            var cutoff = new TimeSpan(9, 15, 0);
            var lateCount = attendance.Count(x => x.EntryTime.HasValue && x.EntryTime.Value.TimeOfDay > cutoff);
            if (lateCount > 0)
            {
                list.Add(new AppNotification
                {
                    Id = $"admin-late-{AppTime.Today:yyyyMMdd}-{lateCount}",
                    Title = "Late arrivals detected",
                    Message = $"{lateCount} employee(s) arrived late today.",
                    Type = NotificationType.Info,
                    Link = "/attendance-admin",
                    CreatedAt = now.AddMinutes(-15)
                });
            }
        }
        catch
        {
        }

        list.Add(new AppNotification
        {
            Id = $"admin-payroll-{DateTime.Today:yyyyMM}",
            Title = "Payroll reminder",
            Message = $"Run payroll for {DateTime.Today:MMMM yyyy} when attendance is finalized.",
            Type = NotificationType.Success,
            Link = "/payroll",
            CreatedAt = now.AddHours(-1)
        });

        return list;
    }

    private async Task<List<AppNotification>> BuildEmployeeNotifications(int employeeId)
    {
        var now = DateTime.Now;
        var list = new List<AppNotification>();

        try
        {
            var month = AppTime.Now.Month;
            var year = AppTime.Now.Year;
            var attendance = await _http.GetFromJsonAsync<List<EmployeeAttendanceVm>>($"api/attendance/monthly?employeeId={employeeId}&month={month}&year={year}") ?? new List<EmployeeAttendanceVm>();
            var todayMarked = attendance.Any(x => x.Date == AppTime.Today);
            if (!todayMarked)
            {
                list.Add(new AppNotification
                {
                    Id = $"emp-attendance-reminder-{AppTime.Today:yyyyMMdd}",
                    Title = "Attendance reminder",
                    Message = "You have not marked attendance for today yet.",
                    Type = NotificationType.Warning,
                    Link = "/attendance",
                    CreatedAt = now
                });
            }
        }
        catch
        {
        }

        try
        {
            var leaves = await _http.GetFromJsonAsync<List<EmployeeLeaveVm>>($"api/leaves/employee/{employeeId}") ?? new List<EmployeeLeaveVm>();
            var latest = leaves
                .OrderByDescending(x => x.LeaveId)
                .FirstOrDefault(x => !string.Equals(x.Status, "Pending", StringComparison.OrdinalIgnoreCase));
            if (latest is not null)
            {
                list.Add(new AppNotification
                {
                    Id = $"emp-leave-{latest.LeaveId}-{latest.Status}",
                    Title = "Leave status updated",
                    Message = $"{latest.LeaveType} request is now {latest.Status}.",
                    Type = string.Equals(latest.Status, "Approved", StringComparison.OrdinalIgnoreCase)
                        ? NotificationType.Success
                        : NotificationType.Info,
                    Link = "/leaves",
                    CreatedAt = now.AddMinutes(-20)
                });
            }
        }
        catch
        {
        }

        try
        {
            var salaries = await _http.GetFromJsonAsync<List<EmployeeSalaryVm>>($"api/salaries/employee/{employeeId}") ?? new List<EmployeeSalaryVm>();
            var latest = salaries.OrderByDescending(x => x.GeneratedOn).FirstOrDefault();
            if (latest is not null)
            {
                list.Add(new AppNotification
                {
                    Id = $"emp-salary-{latest.SalaryId}",
                    Title = "Payslip available",
                    Message = $"Your payslip for {latest.Month} is ready.",
                    Type = NotificationType.Success,
                    Link = "/salaries",
                    CreatedAt = latest.GeneratedOn.ToLocalTime()
                });
            }
        }
        catch
        {
        }

        return list;
    }

    private void HandleAuthChanged()
    {
        _ = RefreshSilentlyAsync();
    }

    private async Task RefreshSilentlyAsync()
    {
        try
        {
            await RefreshAsync();
        }
        catch
        {
            // no-op, keep UI stable
        }
    }

    public void Dispose()
    {
        _authState.OnChange -= HandleAuthChanged;
    }

    private async Task<List<AppNotification>> LoadPortalFeedNotificationsAsync()
    {
        try
        {
            var rows = await _http.GetFromJsonAsync<List<PortalFeedVm>>("api/notifications/feed?take=40") ?? new List<PortalFeedVm>();
            return rows.Select(x => new AppNotification
            {
                Id = $"portal-{x.Id}",
                Title = x.Title,
                Message = x.Message,
                Type = MapPortalType(x.Type),
                Link = string.IsNullOrWhiteSpace(x.Link) ? "/home" : x.Link,
                CreatedAt = x.CreatedAtUtc.Kind == DateTimeKind.Utc
                    ? x.CreatedAtUtc.ToLocalTime()
                    : DateTime.SpecifyKind(x.CreatedAtUtc, DateTimeKind.Utc).ToLocalTime()
            }).ToList();
        }
        catch
        {
            return new List<AppNotification>();
        }
    }

    private static NotificationType MapPortalType(string type)
    {
        if (string.Equals(type, "Success", StringComparison.OrdinalIgnoreCase))
            return NotificationType.Success;
        if (string.Equals(type, "Warning", StringComparison.OrdinalIgnoreCase))
            return NotificationType.Warning;
        return NotificationType.Info;
    }

    private sealed class PortalFeedVm
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("link")]
        public string? Link { get; set; }

        [JsonPropertyName("createdAtUtc")]
        public DateTime CreatedAtUtc { get; set; }
    }

    private sealed class AdminLeaveVm
    {
        public int LeaveId { get; set; }
    }

    private sealed class AdminAttendanceVm
    {
        public DateTime? EntryTime { get; set; }
    }

    private sealed class EmployeeAttendanceVm
    {
        public DateOnly Date { get; set; }
    }

    private sealed class EmployeeLeaveVm
    {
        public int LeaveId { get; set; }
        public string LeaveType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    private sealed class EmployeeSalaryVm
    {
        public int SalaryId { get; set; }
        public string Month { get; set; } = string.Empty;
        public DateTime GeneratedOn { get; set; }
    }
}

public sealed class AppNotification
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; } = NotificationType.Info;
    public string Link { get; set; } = "/";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsRead { get; set; }
}

public enum NotificationType
{
    Info,
    Success,
    Warning
}
