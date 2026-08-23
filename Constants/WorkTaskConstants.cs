namespace HanaMedia.Constants;

public static class WorkTaskStatuses
{
    public const string Todo = "todo";
    public const string InProgress = "in_progress";
    public const string Review = "review";
    public const string NeedRevision = "need_revision";
    public const string Approved = "approved";
    public const string Done = "done";

    public static IReadOnlyList<string> All { get; } =
        Array.AsReadOnly([Todo, InProgress, Review, NeedRevision, Approved, Done]);

    public static string GetLabel(string status) => status switch
    {
        Todo => "Cần làm",
        InProgress => "Đang thực hiện",
        Review => "Chờ duyệt",
        NeedRevision => "Cần chỉnh sửa",
        Approved => "Đã duyệt",
        Done => "Hoàn thành",
        _ => status
    };
}

public static class WorkTaskModules
{
    public const string HumanResources = AuditModules.HumanResources;
    public const string Booking = AuditModules.Booking;
    public const string Ideas = AuditModules.Ideas;

    public static IReadOnlyList<string> All { get; } =
        Array.AsReadOnly([HumanResources, Booking, Ideas]);

    public static bool IsValid(string? module) => module is not null && All.Contains(module);

    public static string GetLabel(string module) => module switch
    {
        HumanResources => "Hành chính nhân sự",
        Booking => "Booking",
        Ideas => "Ý tưởng",
        _ => module
    };

    public static string GetDepartment(string module) => module switch
    {
        HumanResources => "HCNS",
        Booking => "Booking",
        Ideas => "Y_tuong",
        _ => throw new ArgumentOutOfRangeException(nameof(module))
    };
}
