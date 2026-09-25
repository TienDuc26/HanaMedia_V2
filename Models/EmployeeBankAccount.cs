namespace HanaMedia.Models;

public class EmployeeBankAccount
{
    public int EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;

    public string BankName { get; set; } = null!;

    public string AccountNumber { get; set; } = null!;

    public string AccountHolderName { get; set; } = null!;

    public DateTime? UpdatedAt { get; set; }
}
