namespace HanaMedia.Constants;

public static class AuditModules
{
    public const string HumanResources = "Nhan_Su";
    public const string Booking = "Booking";
    public const string Ideas = "Y_Tuong";
    public const string Accounts = "Tai_Khoan";
    public const string Configuration = "Cau_Hinh";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        HumanResources,
        Booking,
        Ideas,
        Accounts,
        Configuration
    };

    public static bool IsValid(string module) => All.Contains(module);
}
