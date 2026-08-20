using HanaMedia.ViewModels;

namespace HanaMedia.Services.Accounts;

public interface IAccountManagementService
{
    Task<AccountManagementPageViewModel> GetPageAsync(
        string? search,
        int? currentUserId,
        CancellationToken cancellationToken = default);

    Task<AccountOperationResult> CreateAccountAsync(
        CreateAccountInputModel input,
        int actorUserId,
        CancellationToken cancellationToken = default);

    Task<AccountOperationResult> ChangeRoleAsync(
        ChangeRoleInputModel input,
        int actorUserId,
        CancellationToken cancellationToken = default);

    Task<AccountOperationResult> SetStatusAsync(
        int userId,
        string targetStatus,
        Guid expectedSecurityStamp,
        int actorUserId,
        CancellationToken cancellationToken = default);

    Task<AccountOperationResult> ResetPasswordAsync(
        int userId,
        Guid expectedSecurityStamp,
        int actorUserId,
        CancellationToken cancellationToken = default);

    Task<LoginHistoryResponseViewModel?> GetLoginHistoryAsync(
        int userId,
        CancellationToken cancellationToken = default);
}
