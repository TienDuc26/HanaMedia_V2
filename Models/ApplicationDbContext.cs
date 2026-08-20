using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace HanaMedia.Models;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Booking> Bookings { get; set; }

    public virtual DbSet<BookingWage> BookingWages { get; set; }

    public virtual DbSet<BookingWageAuditLog> BookingWageAuditLogs { get; set; }

    public virtual DbSet<BusinessConfig> BusinessConfigs { get; set; }

    public virtual DbSet<Employee> Employees { get; set; }

    public virtual DbSet<Idea> Ideas { get; set; }

    public virtual DbSet<Kol> Kols { get; set; }

    public virtual DbSet<SystemAuditLog> SystemAuditLogs { get; set; }

    public virtual DbSet<SystemConfig> SystemConfigs { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Name=ConnectionStrings:DefaultConnection");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__bookings__3213E83F446D00CB");

            entity.ToTable("bookings");

            entity.HasIndex(e => e.Status, "idx_bookings_status");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ActualCost)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("actual_cost");
            entity.Property(e => e.BookingPrice)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("booking_price");
            entity.Property(e => e.CampaignName)
                .HasMaxLength(100)
                .HasColumnName("campaign_name");
            entity.Property(e => e.ClientName)
                .HasMaxLength(100)
                .HasColumnName("client_name");
            entity.Property(e => e.ContractFileUrl)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("contract_file_url");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Deadline).HasColumnName("deadline");
            entity.Property(e => e.JobDescription).HasColumnName("job_description");
            entity.Property(e => e.KolId).HasColumnName("kol_id");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.PostLink)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("post_link");
            entity.Property(e => e.PostingDate).HasColumnName("posting_date");
            entity.Property(e => e.PrimaryManagerId).HasColumnName("primary_manager_id");
            entity.Property(e => e.QuotationFileUrl)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("quotation_file_url");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("dang_cho")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Kol).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.KolId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK__bookings__kol_id__7E37BEF6");

            entity.HasOne(d => d.PrimaryManager).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.PrimaryManagerId)
                .HasConstraintName("FK__bookings__primar__7F2BE32F");
        });

        modelBuilder.Entity<BookingWage>(entity =>
        {
            entity.HasKey(e => new { e.BookingId, e.EmployeeId }).HasName("PK__booking___C1B1450B40E1706B");

            entity.ToTable("booking_wages");

            entity.Property(e => e.BookingId).HasColumnName("booking_id");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.AllocatedWage)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("allocated_wage");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Booking).WithMany(p => p.BookingWages)
                .HasForeignKey(d => d.BookingId)
                .HasConstraintName("FK__booking_w__booki__03F0984C");

            entity.HasOne(d => d.Employee).WithMany(p => p.BookingWages)
                .HasForeignKey(d => d.EmployeeId)
                .HasConstraintName("FK__booking_w__emplo__04E4BC85");
        });

        modelBuilder.Entity<BookingWageAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__booking___3213E83F0CE97CF5");

            entity.ToTable("booking_wage_audit_logs");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.BookingId).HasColumnName("booking_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.LogDetail)
                .HasMaxLength(255)
                .HasColumnName("log_detail");
            entity.Property(e => e.PerformedByUserId).HasColumnName("performed_by_user_id");

            entity.HasOne(d => d.Booking).WithMany(p => p.BookingWageAuditLogs)
                .HasForeignKey(d => d.BookingId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__booking_w__booki__08B54D69");

            entity.HasOne(d => d.PerformedByUser).WithMany(p => p.BookingWageAuditLogs)
                .HasForeignKey(d => d.PerformedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK__booking_w__perfo__09A971A2");
        });

        modelBuilder.Entity<BusinessConfig>(entity =>
        {
            entity.HasKey(e => e.ConfigKey).HasName("PK__business__BDF6033CB924E59B");

            entity.ToTable("business_configs");

            entity.Property(e => e.ConfigKey)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("config_key");
            entity.Property(e => e.ConfigValue)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("config_value");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__employee__3213E83FCE719B40");

            entity.ToTable("employees");

            entity.HasIndex(e => e.Email, "UQ__employee__AB6E616482BC8677").IsUnique();

            entity.HasIndex(e => e.UserId, "UQ__employee__B9BE370E68AE089C").IsUnique();

            entity.HasIndex(e => e.Department, "idx_employees_dept");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Address)
                .HasMaxLength(255)
                .HasColumnName("address");
            entity.Property(e => e.Allowance)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("allowance");
            entity.Property(e => e.AvatarUrl)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("avatar_url");
            entity.Property(e => e.BasicSalary)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("basic_salary");
            entity.Property(e => e.ContractType)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("contract_type");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Department)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("department");
            entity.Property(e => e.Dob).HasColumnName("dob");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("email");
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .HasColumnName("full_name");
            entity.Property(e => e.JoinedDate).HasColumnName("joined_date");
            entity.Property(e => e.ManagerId).HasColumnName("manager_id");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("phone");
            entity.Property(e => e.Position)
                .HasMaxLength(100)
                .HasColumnName("position");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("thu_viec")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Manager).WithMany(p => p.InverseManager)
                .HasForeignKey(d => d.ManagerId)
                .HasConstraintName("FK__employees__manag__6EF57B66");

            entity.HasOne(d => d.User).WithOne(p => p.Employee)
                .HasForeignKey<Employee>(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK__employees__user___6E01572D");
        });

        modelBuilder.Entity<Idea>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ideas__3213E83F62C66024");

            entity.ToTable("ideas");

            entity.HasIndex(e => e.Status, "idx_ideas_status");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CampaignName)
                .HasMaxLength(100)
                .HasColumnName("campaign_name");
            entity.Property(e => e.Category)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("category");
            entity.Property(e => e.ClientName)
                .HasMaxLength(100)
                .HasColumnName("client_name");
            entity.Property(e => e.Concept).HasColumnName("concept");
            entity.Property(e => e.ContentDetails).HasColumnName("content_details");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatorEmployeeId).HasColumnName("creator_employee_id");
            entity.Property(e => e.Deadline).HasColumnName("deadline");
            entity.Property(e => e.FeedbackComment).HasColumnName("feedback_comment");
            entity.Property(e => e.Industry)
                .HasMaxLength(100)
                .HasColumnName("industry");
            entity.Property(e => e.Insight).HasColumnName("insight");
            entity.Property(e => e.MoodboardDesc).HasColumnName("moodboard_desc");
            entity.Property(e => e.PrimaryStaffId).HasColumnName("primary_staff_id");
            entity.Property(e => e.ReferenceLink)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("reference_link");
            entity.Property(e => e.ReviewerEmployeeId).HasColumnName("reviewer_employee_id");
            entity.Property(e => e.ScriptText).HasColumnName("script_text");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("y_tuong")
                .HasColumnName("status");
            entity.Property(e => e.Title)
                .HasMaxLength(150)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.CreatorEmployee).WithMany(p => p.IdeaCreatorEmployees)
                .HasForeignKey(d => d.CreatorEmployeeId)
                .HasConstraintName("FK__ideas__creator_e__114A936A");

            entity.HasOne(d => d.PrimaryStaff).WithMany(p => p.IdeaPrimaryStaffs)
                .HasForeignKey(d => d.PrimaryStaffId)
                .HasConstraintName("FK__ideas__primary_s__123EB7A3");

            entity.HasOne(d => d.ReviewerEmployee).WithMany(p => p.IdeaReviewerEmployees)
                .HasForeignKey(d => d.ReviewerEmployeeId)
                .HasConstraintName("FK__ideas__reviewer___1332DBDC");
        });

        modelBuilder.Entity<Kol>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__kols__3213E83F79266D0D");

            entity.ToTable("kols");

            entity.HasIndex(e => e.Platform, "idx_kols_platform");

            entity.HasIndex(e => e.Status, "idx_kols_status");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.BookingPrice)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("booking_price");
            entity.Property(e => e.ContactInfo)
                .HasMaxLength(255)
                .HasColumnName("contact_info");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.EngagementRate)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("engagement_rate");
            entity.Property(e => e.FollowersCount).HasColumnName("followers_count");
            entity.Property(e => e.Location)
                .HasMaxLength(100)
                .HasColumnName("location");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.Niche)
                .HasMaxLength(100)
                .HasColumnName("niche");
            entity.Property(e => e.Platform)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("platform");
            entity.Property(e => e.ProfileLink)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("profile_link");
            entity.Property(e => e.RatingScore).HasColumnName("rating_score");
            entity.Property(e => e.ResponsibleStaffId).HasColumnName("responsible_staff_id");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("tiem_nang")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.ResponsibleStaff).WithMany(p => p.Kols)
                .HasForeignKey(d => d.ResponsibleStaffId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK__kols__responsibl__778AC167");
        });

        modelBuilder.Entity<SystemAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__system_a__3213E83F2A038E81");

            entity.ToTable("system_audit_logs");

            entity.HasIndex(e => e.CreatedAt, "idx_audit_logs_created");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ActionType)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("action_type");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.DeviceInfo)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("device_info");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .IsUnicode(false)
                .HasColumnName("ip_address");
            entity.Property(e => e.LogDetail).HasColumnName("log_detail");
            entity.Property(e => e.Module)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("module");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.SystemAuditLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK__system_au__user___17F790F9");
        });

        modelBuilder.Entity<SystemConfig>(entity =>
        {
            entity.HasKey(e => e.ConfigKey).HasName("PK__system_c__BDF6033C8CA1087D");

            entity.ToTable("system_configs");

            entity.Property(e => e.ConfigKey)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("config_key");
            entity.Property(e => e.ConfigValue)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("config_value");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__users__3213E83F462D2B60");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "UQ__users__AB6E6164982D099A").IsUnique();

            entity.HasIndex(e => e.Username, "UQ__users__F3DBC57222B3371D").IsUnique();

            entity.HasIndex(e => e.Role, "idx_users_role");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("email");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("password_hash");
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("role");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("active")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("username");

            entity.Property(e => e.FailedLoginAttempts)
                .HasDefaultValue(0)
                .HasColumnName("failed_login_attempts");

            entity.Property(e => e.LockedUntil).HasColumnName("locked_until");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
