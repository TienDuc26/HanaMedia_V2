using HanaMedia.Models.Dto;

namespace HanaMedia.Services.Profile;

public interface IProfileService
{
    Task<ProfileViewDto?> GetProfileForEmployeeAsync(int employeeId, CancellationToken ct = default);

    Task<bool> UpdatePersonalInfoAsync(
        int employeeId,
        EmployeePersonalInfoUpdateDto input,
        string? newUsersEmail,
        CancellationToken ct = default);

    Task<bool> UpdateBankAccountAsync(
        int employeeId,
        EmployeeBankAccountUpdateDto input,
        CancellationToken ct = default);
}
