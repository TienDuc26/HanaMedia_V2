namespace HanaMedia.Constants;

public static class AuditActions
{
    public const string LoginSucceeded = "login_succeeded";
    public const string LoginFailed = "login_failed";
    public const string LoginBlockedNetwork = "login_blocked_network";
    public const string Logout = "logout";
    public const string AccountAutoLocked = "account_auto_locked";
    public const string Created = "create";
    public const string Updated = "update";
    public const string Deleted = "delete";
    public const string Approved = "approve";
    public const string Rejected = "reject";
    public const string ContractSigned = "contract_signed";
    public const string WageChanged = "wage_changed";
    public const string BulkDelete = "bulk_delete";
}
