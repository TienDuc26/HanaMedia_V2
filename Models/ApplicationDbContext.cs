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

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<Employee> Employees { get; set; }

    public virtual DbSet<Idea> Ideas { get; set; }

    public virtual DbSet<Kol> Kols { get; set; }

    public virtual DbSet<SystemAuditLog> SystemAuditLogs { get; set; }

    public virtual DbSet<SystemConfig> SystemConfigs { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Name=ConnectionStrings:DefaultConnection");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_bookings");

            entity.ToTable("bookings", table =>
                table.HasCheckConstraint(
                    "chk_booking_status",
                    "[status] IN ('dang_cho', 'thuong_luong', 'da_chot', 'dang_trien_khai', 'hoan_thanh', 'huy')"));

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
                .HasConstraintName("FK_bookings_kols_kol_id");

            entity.HasOne(d => d.PrimaryManager).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.PrimaryManagerId)
                .HasConstraintName("FK_bookings_employees_primary_manager_id");
        });

        modelBuilder.Entity<BookingWage>(entity =>
        {
            entity.HasKey(e => new { e.BookingId, e.EmployeeId }).HasName("PK_booking_wages");

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
                .HasConstraintName("FK_booking_wages_bookings_booking_id");

            entity.HasOne(d => d.Employee).WithMany(p => p.BookingWages)
                .HasForeignKey(d => d.EmployeeId)
                .HasConstraintName("FK_booking_wages_employees_employee_id");
        });

        modelBuilder.Entity<BookingWageAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_booking_wage_audit_logs");

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
                .HasConstraintName("FK_booking_wage_audit_logs_bookings_booking_id");

            entity.HasOne(d => d.PerformedByUser).WithMany(p => p.BookingWageAuditLogs)
                .HasForeignKey(d => d.PerformedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_booking_wage_audit_logs_users_performed_by_user_id");
        });

        modelBuilder.Entity<BusinessConfig>(entity =>
        {
            entity.HasKey(e => e.ConfigKey).HasName("PK_business_configs");

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
            entity.HasKey(e => e.Id).HasName("PK_employees");

            entity.ToTable("employees", table =>
            {
                table.HasCheckConstraint(
                    "chk_emp_dept",
                    "[department] IN ('HCNS', 'Booking', 'Y_tuong', 'IT')");
                table.HasCheckConstraint(
                    "chk_emp_contract",
                    "[contract_type] IN ('thu_viec', 'chinh_thuc_1_nam', 'vo_thoi_han')");
                table.HasCheckConstraint(
                    "chk_emp_status",
                    "[status] IN ('dang_lam_viec', 'thu_viec', 'cho_duyet_nghi', 'ngung_hoat_dong')");
            });

            entity.HasIndex(e => e.Email, "UX_employees_email").IsUnique();

            entity.HasIndex(e => e.UserId, "UX_employees_user_id")
                .IsUnique()
                .HasFilter("[user_id] IS NOT NULL");

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
            entity.Property(e => e.IsManager)
                .HasColumnName("is_manager")
                .HasDefaultValue(false);
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
                .HasConstraintName("FK_employees_employees_manager_id");

            entity.HasOne(d => d.User).WithOne(p => p.Employee)
                .HasForeignKey<Employee>(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_employees_users_user_id");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_departments");

            entity.ToTable("departments");

            entity.HasIndex(e => e.Code, "UX_departments_code").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .HasDefaultValue("active")
                .IsRequired();
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<Idea>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_ideas");

            entity.ToTable("ideas", table =>
            {
                table.HasCheckConstraint(
                    "chk_idea_cat",
                    "[category] IN ('trend', 'viral', 'da_trien_khai', 'chua_su_dung')");
                table.HasCheckConstraint(
                    "chk_idea_status",
                    "[status] IN ('y_tuong', 'review', 'need_revision', 'approved', 'done')");
            });

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
                .HasConstraintName("FK_ideas_employees_creator_employee_id");

            entity.HasOne(d => d.PrimaryStaff).WithMany(p => p.IdeaPrimaryStaffs)
                .HasForeignKey(d => d.PrimaryStaffId)
                .HasConstraintName("FK_ideas_employees_primary_staff_id");

            entity.HasOne(d => d.ReviewerEmployee).WithMany(p => p.IdeaReviewerEmployees)
                .HasForeignKey(d => d.ReviewerEmployeeId)
                .HasConstraintName("FK_ideas_employees_reviewer_employee_id");
        });

        modelBuilder.Entity<Kol>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_kols");

            entity.ToTable("kols", table =>
            {
                table.HasCheckConstraint(
                    "chk_kol_platform",
                    "[platform] IN ('TikTok', 'Instagram', 'YouTube', 'Facebook')");
                table.HasCheckConstraint(
                    "chk_kol_rating",
                    "[rating_score] BETWEEN 1 AND 5");
                table.HasCheckConstraint(
                    "chk_kol_status",
                    "[status] IN ('tiem_nang', 'da_lien_he', 'dang_deal', 'da_chot', 'dang_chay', 'hoan_thanh')");
            });

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
                .HasConstraintName("FK_kols_employees_responsible_staff_id");
        });

        modelBuilder.Entity<SystemAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_system_audit_logs");

            entity.ToTable("system_audit_logs", table =>
                table.HasCheckConstraint(
                    "chk_log_module",
                    "[module] IN ('Nhan_Su', 'Booking', 'Y_Tuong', 'Tai_Khoan', 'Cau_Hinh')"));

            entity.HasIndex(e => e.CreatedAt, "idx_audit_logs_created");

            entity.HasIndex(
                    e => new { e.UserId, e.CreatedAt },
                    "idx_audit_logs_login_history")
                .IsDescending(false, true)
                .HasFilter("[module] = 'Tai_Khoan' AND [user_id] IS NOT NULL")
                .IncludeProperties(e => new { e.ActionType, e.IpAddress, e.DeviceInfo });

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
                .HasConstraintName("FK_system_audit_logs_users_user_id");
        });

        modelBuilder.Entity<SystemConfig>(entity =>
        {
            entity.HasKey(e => e.ConfigKey).HasName("PK_system_configs");

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
            entity.HasKey(e => e.Id).HasName("PK_users");

            entity.ToTable("users", table =>
            {
                table.HasCheckConstraint(
                    "chk_user_role",
                    "[role] IN ('giam_doc', 'admin_it', 'ql_hcns', 'nv_hcns', 'ql_booking', 'nv_booking', 'ql_y_tuong', 'nv_y_tuong')");
                table.HasCheckConstraint(
                    "chk_user_status",
                    "[status] IN ('active', 'locked')");
            });

            entity.HasIndex(e => e.Email, "UX_users_email").IsUnique();

            entity.HasIndex(e => e.Username, "UX_users_username").IsUnique();

            entity.HasIndex(e => e.Role, "idx_users_role");

            entity.HasIndex(e => new { e.Status, e.Role }, "idx_users_status_role")
                .IncludeProperties(e => new { e.Username, e.Email });

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
            entity.Property(e => e.SecurityStamp)
                .HasColumnType("nvarchar(max)")
                .IsConcurrencyToken()
                .HasColumnName("security_stamp");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .IsRequired()
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
