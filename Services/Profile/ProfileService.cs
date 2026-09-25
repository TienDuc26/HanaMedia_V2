using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HanaMedia.Constants;
using HanaMedia.Models;
using HanaMedia.Models.Dto;
using HanaMedia.Services.Auditing;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Services.Profile;

public class ProfileService : IProfileService
{
    private readonly ApplicationDbContext _db;
    private readonly EmployeeAvatarService _avatarService;
    private readonly ISystemAuditService _audit;

    public ProfileService(
        ApplicationDbContext db,
        EmployeeAvatarService avatarService,
        ISystemAuditService audit)
    {
        _db = db;
        _avatarService = avatarService;
        _audit = audit;
    }

    public async Task<ProfileViewDto?> GetProfileForEmployeeAsync(int employeeId, CancellationToken ct = default)
    {
        var emp = await _db.Employees
            .Include(e => e.Manager)
            .Include(e => e.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeeId, ct);

        if (emp == null) return null;

        string? departmentName = null;
        if (!string.IsNullOrEmpty(emp.Department))
        {
            departmentName = await _db.Departments
                .Where(d => d.Code == emp.Department)
                .Select(d => d.Name)
                .FirstOrDefaultAsync(ct);
        }

        EmployeeBankAccountDto? bankDto = null;
        var bank = await _db.EmployeeBankAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.EmployeeId == employeeId, ct);
        if (bank != null)
        {
            bankDto = new EmployeeBankAccountDto
            {
                BankName = bank.BankName,
                AccountNumber = bank.AccountNumber,
                AccountHolderName = bank.AccountHolderName,
                UpdatedAt = bank.UpdatedAt
            };
        }

        return new ProfileViewDto
        {
            EmployeeId = emp.Id,
            FullName = emp.FullName,
            AvatarUrl = emp.AvatarUrl,
            QrCodeUrl = emp.User?.QrCodeUrl,
            Dob = emp.Dob,
            Phone = emp.Phone,
            Email = emp.Email,
            Address = emp.Address,
            JoinedDate = emp.JoinedDate,
            Department = emp.Department,
            DepartmentName = departmentName ?? emp.Department,
            Position = emp.Position,
            ManagerName = emp.Manager?.FullName,
            ContractType = emp.ContractType,
            ContractTypeLabel = ContractTypeLabel(emp.ContractType),
            Status = emp.Status,
            StatusLabel = StatusLabel(emp.Status),
            BankAccount = bankDto
        };
    }

    public async Task<bool> UpdatePersonalInfoAsync(
        int employeeId,
        EmployeePersonalInfoUpdateDto input,
        string? newUsersEmail,
        CancellationToken ct = default)
    {
        var emp = await _db.Employees.FindAsync(new object[] { employeeId }, ct);
        if (emp == null) return false;

        bool changed = false;

        if (input.FullName != null && input.FullName.Trim() != emp.FullName)
        {
            emp.FullName = input.FullName.Trim();
            changed = true;
        }

        if (input.Dob.HasValue)
        {
            if (input.Dob.Value > DateOnly.FromDateTime(DateTime.Today))
                throw new InvalidOperationException("Ngày sinh không được lớn hơn ngày hiện tại.");
            if (input.Dob.Value != emp.Dob)
            {
                emp.Dob = input.Dob.Value;
                changed = true;
            }
        }

        if (input.Phone != null && input.Phone.Trim() != emp.Phone)
        {
            emp.Phone = input.Phone.Trim();
            changed = true;
        }

        if (input.Email != null && input.Email.Trim() != emp.Email)
        {
            emp.Email = input.Email.Trim();
            changed = true;
        }

        if (input.Address != null && input.Address.Trim() != emp.Address)
        {
            emp.Address = input.Address.Trim();
            changed = true;
        }

        if (input.AvatarBase64 != null)
        {
            var newUrl = await _avatarService.SaveAsync(emp.Id, input.AvatarBase64);
            if (newUrl != null)
            {
                emp.AvatarUrl = newUrl;
                changed = true;
            }
        }

        // Update email của AspNetUser (table users) — áp dụng cùng transaction với Employees update
        if (!string.IsNullOrWhiteSpace(newUsersEmail) && emp.UserId != null)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == emp.UserId, ct);
            if (user != null && !string.Equals(user.Email, newUsersEmail, StringComparison.OrdinalIgnoreCase))
            {
                user.Email = newUsersEmail;
                changed = true;
            }
        }

        if (!changed) return true;

        emp.UpdatedAt = DateTime.UtcNow;

        // Gộp cập nhật employees + users + audit vào MỘT SaveChanges
        _audit.AddEvent(new AuditEvent(
            AuditModules.HumanResources,
            "profile_updated",
            $"Cập nhật thông tin cá nhân của nhân viên ID={emp.Id} ({emp.FullName})",
            TargetType: "employees",
            TargetId: emp.Id.ToString()));

        await _db.SaveChangesAsync(ct);

        return true;
    }

    public async Task<bool> UpdateBankAccountAsync(
        int employeeId,
        EmployeeBankAccountUpdateDto input,
        CancellationToken ct = default)
    {
        var emp = await _db.Employees.FindAsync(new object[] { employeeId }, ct);
        if (emp == null) return false;

        var bank = await _db.EmployeeBankAccounts.FindAsync(new object[] { employeeId }, ct);

        if (bank == null)
        {
            bank = new EmployeeBankAccount { EmployeeId = employeeId };
            _db.EmployeeBankAccounts.Add(bank);
        }

        bank.BankName = input.BankName.Trim();
        bank.AccountNumber = input.AccountNumber.Trim();
        bank.AccountHolderName = input.AccountHolderName.Trim();
        bank.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _audit.AddEvent(new AuditEvent(
            AuditModules.HumanResources,
            "bank_account_updated",
            $"Cập nhật tài khoản nhận lương của nhân viên ID={emp.Id} ({emp.FullName})",
            TargetType: "employee_bank_accounts",
            TargetId: emp.Id.ToString()));

        await _db.SaveChangesAsync(ct);

        return true;
    }

    private static string ContractTypeLabel(string? ct) => ct switch
    {
        "thu_viec" => "Thử việc",
        "chinh_thuc_1_nam" => "Hợp đồng chính thức (1 năm)",
        "vo_thoi_han" => "Hợp đồng vô thời hạn",
        _ => ct ?? ""
    };

    private static string StatusLabel(string? s) => s switch
    {
        "dang_lam_viec" => "Đang làm việc",
        "thu_viec" => "Thử việc",
        "tam_ngung" => "Tạm ngừng",
        "ngung_hoat_dong" => "Đã nghỉ việc",
        _ => s ?? ""
    };
}
