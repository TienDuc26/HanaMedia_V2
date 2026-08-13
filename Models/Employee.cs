using System;
using System.Collections.Generic;

namespace HanaMedia.Models;

public partial class Employee
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string? AvatarUrl { get; set; }

    public DateOnly Dob { get; set; }

    public string Phone { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Address { get; set; } = null!;

    public DateOnly JoinedDate { get; set; }

    public string Department { get; set; } = null!;

    public string Position { get; set; } = null!;

    public int? ManagerId { get; set; }

    public string ContractType { get; set; } = null!;

    public decimal BasicSalary { get; set; }

    public decimal? Allowance { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<BookingWage> BookingWages { get; set; } = new List<BookingWage>();

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual ICollection<Idea> IdeaCreatorEmployees { get; set; } = new List<Idea>();

    public virtual ICollection<Idea> IdeaPrimaryStaffs { get; set; } = new List<Idea>();

    public virtual ICollection<Idea> IdeaReviewerEmployees { get; set; } = new List<Idea>();

    public virtual ICollection<Employee> InverseManager { get; set; } = new List<Employee>();

    public virtual ICollection<Kol> Kols { get; set; } = new List<Kol>();

    public virtual Employee? Manager { get; set; }

    public virtual User? User { get; set; }
}
