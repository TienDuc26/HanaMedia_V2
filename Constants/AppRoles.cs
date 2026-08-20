using System.Collections.ObjectModel;

namespace HanaMedia.Constants;

public static class AppRoles
{
    public const string Director = "giam_doc";
    public const string AdminIT = "admin_it";
    public const string HumanResourcesManager = "ql_hcns";
    public const string HumanResourcesStaff = "nv_hcns";
    public const string BookingManager = "ql_booking";
    public const string BookingStaff = "nv_booking";
    public const string IdeaManager = "ql_y_tuong";
    public const string IdeaStaff = "nv_y_tuong";

    private static readonly IReadOnlyDictionary<string, string> RoleLabels =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [Director] = "Giám đốc",
                [AdminIT] = "AdminIT",
                [HumanResourcesManager] = "Quản lý HCNS",
                [HumanResourcesStaff] = "Nhân viên HCNS",
                [BookingManager] = "Quản lý Booking",
                [BookingStaff] = "Nhân viên Booking",
                [IdeaManager] = "Quản lý Ý tưởng",
                [IdeaStaff] = "Nhân viên Ý tưởng"
            });

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(
    [
        Director,
        AdminIT,
        HumanResourcesManager,
        HumanResourcesStaff,
        BookingManager,
        BookingStaff,
        IdeaManager,
        IdeaStaff
    ]);

    public static IReadOnlyDictionary<string, string> Labels => RoleLabels;

    public static bool IsValid(string? role) =>
        role is not null && RoleLabels.ContainsKey(role);

    public static bool TryGetLabel(string? role, out string label)
    {
        if (role is not null && RoleLabels.TryGetValue(role, out var resolvedLabel))
        {
            label = resolvedLabel;
            return true;
        }

        label = string.Empty;
        return false;
    }

    public static string GetLabel(string role) =>
        RoleLabels.TryGetValue(role, out var label)
            ? label
            : throw new ArgumentOutOfRangeException(nameof(role), role, "Vai trò không hợp lệ.");
}

public static class AccountStatuses
{
    public const string Active = "active";
    public const string Locked = "locked";

    public static IReadOnlyList<string> All { get; } =
        Array.AsReadOnly([Active, Locked]);

    public static bool IsValid(string? status) =>
        string.Equals(status, Active, StringComparison.Ordinal) ||
        string.Equals(status, Locked, StringComparison.Ordinal);
}

