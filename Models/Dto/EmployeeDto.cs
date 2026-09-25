using System;
using System.Collections.Generic;

namespace HanaMedia.Models.Dto;

public class EmployeeDto
{
    public int? Id { get; set; }
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
    public DateOnly Dob { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public DateOnly JoinedDate { get; set; }
    public string? Department { get; set; }
    public string? Position { get; set; }
    public int? ManagerId { get; set; }
    /// <summary>
    /// Cờ cho biết nhân sự này thuộc nhóm chức vụ quản lý (dùng cho dropdown "Người quản lý trực tiếp").
    /// </summary>
    public bool IsManager { get; set; }
    public string? ContractType { get; set; }
    public string? Status { get; set; }
}

public class EmployeeListItemDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public DateOnly Dob { get; set; }
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Department { get; set; } = "";
    public string DepartmentName { get; set; } = "";
    public string Position { get; set; } = "";
    public string? ManagerName { get; set; }
    public string ContractType { get; set; } = "";
    public string ContractTypeLabel { get; set; } = "";
    public string Status { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public DateOnly JoinedDate { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class EmployeeDetailDto : EmployeeListItemDto
{
    public string? Address { get; set; }
}

public class EmployeeCreateUpdateDto
{
    public int? Id { get; set; }
    public string FullName { get; set; } = "";
    public DateOnly Dob { get; set; }
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public DateOnly JoinedDate { get; set; }
    public string Department { get; set; } = "";
    public string Position { get; set; } = "";
    public int? ManagerId { get; set; }
    /// <string>
    /// Cờ cấp quản lý. Khi true: nhân sự này có thể được chọn làm "Người quản lý trực tiếp"
    /// cho các nhân viên khác. Cấu hình thủ công, không suy ra từ chuỗi Position.
    /// Mặc định false.
    /// </string>
    public bool IsManager { get; set; }
    public string ContractType { get; set; } = "";
    public string? Status { get; set; }

    /// <summary>
    /// Ảnh đại diện dạng data URL base64, ví dụ: "data:image/png;base64,iVBORw0KGgo..."
    /// hoặc chuỗi base64 thuần. Null/empty = không đổi avatar.
    /// Khi Create: nếu có thì lưu file + set AvatarUrl.
    /// Khi Update: nếu có thì thay file cũ.
    /// </summary>
    public string? AvatarBase64 { get; set; }
}

public class EmployeeBankAccountDto
{
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountHolderName { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class EmployeeBankAccountUpdateDto
{
    public string BankName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccountHolderName { get; set; } = "";
}

public class EmployeePersonalInfoUpdateDto
{
    public string? FullName { get; set; }
    public DateOnly? Dob { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? AvatarBase64 { get; set; }
}

public class UserUpdateDto
{
    public string? Email { get; set; }
    public string? AvatarBase64 { get; set; }
    public string? QrCodeBase64 { get; set; }
}

public class ProfileViewDto
{
    public int EmployeeId { get; set; }
    public string FullName { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public string? QrCodeUrl { get; set; }
    public DateOnly Dob { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public DateOnly JoinedDate { get; set; }
    public string Department { get; set; } = "";
    public string DepartmentName { get; set; } = "";
    public string Position { get; set; } = "";
    public string? ManagerName { get; set; }
    public string ContractType { get; set; } = "";
    public string ContractTypeLabel { get; set; } = "";
    public string Status { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public EmployeeBankAccountDto? BankAccount { get; set; }
    public bool CanEditPersonalInfo { get; set; }
    public bool CanEditBankAccount { get; set; }
    public bool CanViewBankAccount { get; set; }
}
