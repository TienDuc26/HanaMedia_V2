using System.ComponentModel.DataAnnotations;

namespace HanaMedia.ViewModels;

public sealed class AccountManagementPageViewModel
{
    public IReadOnlyList<AccountListItemViewModel> Accounts { get; init; } = [];

    public IReadOnlyList<EmployeeOptionViewModel> EligibleEmployees { get; init; } = [];

    public IReadOnlyList<RoleOptionViewModel> Roles { get; init; } = [];

    public string? Search { get; init; }

    public string? SuccessMessage { get; set; }

    public string? ErrorMessage { get; set; }

    public string? TemporaryPassword { get; set; }

    public string? TemporaryPasswordUsername { get; set; }
}

public sealed class AccountListItemViewModel
{
    public int Id { get; init; }

    public string Username { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string RoleCode { get; init; } = string.Empty;

    public string RoleName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public Guid SecurityStamp { get; init; }

    public DateTime? LastLoginAt { get; init; }

    public string? LastLoginIp { get; init; }

    public bool IsCurrentAccount { get; init; }

    public bool CanLock { get; init; }

    public bool CanChangeRole { get; init; }
}

public sealed class EmployeeOptionViewModel
{
    public int Id { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string SuggestedUsername { get; init; } = string.Empty;
}

public sealed record RoleOptionViewModel(string Code, string Name);

public sealed class CreateAccountInputModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn nhân viên cần cấp tài khoản.")]
    public int EmployeeId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
    public string Role { get; set; } = string.Empty;
}

public sealed class ChangeRoleInputModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Mã tài khoản không hợp lệ.")]
    public int UserId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
    public string Role { get; set; } = string.Empty;

    public Guid ExpectedSecurityStamp { get; set; }
}

public sealed class AccountStatusInputModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Mã tài khoản không hợp lệ.")]
    public int UserId { get; set; }

    [Required(ErrorMessage = "Trạng thái tài khoản không hợp lệ.")]
    public string Status { get; set; } = string.Empty;

    public Guid ExpectedSecurityStamp { get; set; }
}

public sealed class AccountIdInputModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Mã tài khoản không hợp lệ.")]
    public int UserId { get; set; }

    public Guid ExpectedSecurityStamp { get; set; }
}

public sealed class LoginHistoryResponseViewModel
{
    public string DisplayName { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public IReadOnlyList<LoginHistoryItemViewModel> Items { get; init; } = [];
}

public sealed class LoginHistoryItemViewModel
{
    public string OccurredAt { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string IpAddress { get; init; } = string.Empty;

    public string DeviceInfo { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;
}

public sealed record AccountOperationResult(
    bool Succeeded,
    string Message,
    string? TemporaryPassword = null,
    string? Username = null);
