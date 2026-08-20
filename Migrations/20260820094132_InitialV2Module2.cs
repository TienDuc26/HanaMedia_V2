using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HanaMedia.Migrations
{
    /// <inheritdoc />
    public partial class InitialV2Module2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "business_configs",
                columns: table => new
                {
                    config_key = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    config_value = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_configs", x => x.config_key);
                });

            migrationBuilder.CreateTable(
                name: "system_configs",
                columns: table => new
                {
                    config_key = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    config_value = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_configs", x => x.config_key);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    username = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    role = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false, defaultValue: "active"),
                    security_stamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    updated_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.CheckConstraint("chk_user_role", "[role] IN ('giam_doc', 'admin_it', 'ql_hcns', 'nv_hcns', 'ql_booking', 'nv_booking', 'ql_y_tuong', 'nv_y_tuong')");
                    table.CheckConstraint("chk_user_status", "[status] IN ('active', 'locked')");
                });

            migrationBuilder.CreateTable(
                name: "employees",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    full_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    avatar_url = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    dob = table.Column<DateOnly>(type: "date", nullable: false),
                    phone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    joined_date = table.Column<DateOnly>(type: "date", nullable: false),
                    department = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    position = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    manager_id = table.Column<int>(type: "int", nullable: true),
                    contract_type = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    basic_salary = table.Column<decimal>(type: "decimal(15,2)", nullable: false),
                    allowance = table.Column<decimal>(type: "decimal(15,2)", nullable: true, defaultValue: 0.00m),
                    status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true, defaultValue: "thu_viec"),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    updated_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employees", x => x.id);
                    table.CheckConstraint("chk_emp_contract", "[contract_type] IN ('thu_viec', 'chinh_thuc_1_nam', 'vo_thoi_han')");
                    table.CheckConstraint("chk_emp_dept", "[department] IN ('HCNS', 'Booking', 'Y_tuong', 'IT')");
                    table.CheckConstraint("chk_emp_status", "[status] IN ('dang_lam_viec', 'thu_viec', 'cho_duyet_nghi', 'ngung_hoat_dong')");
                    table.ForeignKey(
                        name: "FK_employees_employees_manager_id",
                        column: x => x.manager_id,
                        principalTable: "employees",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_employees_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "system_audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    action_type = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    module = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    log_detail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ip_address = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: false),
                    device_info = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_audit_logs", x => x.id);
                    table.CheckConstraint("chk_log_module", "[module] IN ('Nhan_Su', 'Booking', 'Y_Tuong', 'Tai_Khoan', 'Cau_Hinh')");
                    table.ForeignKey(
                        name: "FK_system_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ideas",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    creator_employee_id = table.Column<int>(type: "int", nullable: true),
                    client_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    campaign_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    industry = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    category = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    insight = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    concept = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    content_details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    reference_link = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    moodboard_desc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    script_text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    deadline = table.Column<DateOnly>(type: "date", nullable: false),
                    primary_staff_id = table.Column<int>(type: "int", nullable: true),
                    reviewer_employee_id = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true, defaultValue: "y_tuong"),
                    feedback_comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    updated_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ideas", x => x.id);
                    table.CheckConstraint("chk_idea_cat", "[category] IN ('trend', 'viral', 'da_trien_khai', 'chua_su_dung')");
                    table.CheckConstraint("chk_idea_status", "[status] IN ('y_tuong', 'review', 'need_revision', 'approved', 'done')");
                    table.ForeignKey(
                        name: "FK_ideas_employees_creator_employee_id",
                        column: x => x.creator_employee_id,
                        principalTable: "employees",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_ideas_employees_primary_staff_id",
                        column: x => x.primary_staff_id,
                        principalTable: "employees",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_ideas_employees_reviewer_employee_id",
                        column: x => x.reviewer_employee_id,
                        principalTable: "employees",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "kols",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    platform = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    profile_link = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    followers_count = table.Column<int>(type: "int", nullable: false),
                    engagement_rate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    niche = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    booking_price = table.Column<decimal>(type: "decimal(15,2)", nullable: false),
                    location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    contact_info = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    responsible_staff_id = table.Column<int>(type: "int", nullable: true),
                    rating_score = table.Column<byte>(type: "tinyint", nullable: true),
                    status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true, defaultValue: "tiem_nang"),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    updated_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kols", x => x.id);
                    table.CheckConstraint("chk_kol_platform", "[platform] IN ('TikTok', 'Instagram', 'YouTube', 'Facebook')");
                    table.CheckConstraint("chk_kol_rating", "[rating_score] BETWEEN 1 AND 5");
                    table.CheckConstraint("chk_kol_status", "[status] IN ('tiem_nang', 'da_lien_he', 'dang_deal', 'da_chot', 'dang_chay', 'hoan_thanh')");
                    table.ForeignKey(
                        name: "FK_kols_employees_responsible_staff_id",
                        column: x => x.responsible_staff_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    client_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    campaign_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    kol_id = table.Column<int>(type: "int", nullable: true),
                    job_description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    deadline = table.Column<DateOnly>(type: "date", nullable: false),
                    posting_date = table.Column<DateOnly>(type: "date", nullable: true),
                    booking_price = table.Column<decimal>(type: "decimal(15,2)", nullable: false),
                    actual_cost = table.Column<decimal>(type: "decimal(15,2)", nullable: false),
                    primary_manager_id = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true, defaultValue: "dang_cho"),
                    contract_file_url = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    quotation_file_url = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    post_link = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    updated_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.id);
                    table.CheckConstraint("chk_booking_status", "[status] IN ('dang_cho', 'thuong_luong', 'da_chot', 'dang_trien_khai', 'hoan_thanh', 'huy')");
                    table.ForeignKey(
                        name: "FK_bookings_employees_primary_manager_id",
                        column: x => x.primary_manager_id,
                        principalTable: "employees",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_bookings_kols_kol_id",
                        column: x => x.kol_id,
                        principalTable: "kols",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "booking_wage_audit_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    booking_id = table.Column<int>(type: "int", nullable: true),
                    performed_by_user_id = table.Column<int>(type: "int", nullable: true),
                    log_detail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_wage_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_booking_wage_audit_logs_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_booking_wage_audit_logs_users_performed_by_user_id",
                        column: x => x.performed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "booking_wages",
                columns: table => new
                {
                    booking_id = table.Column<int>(type: "int", nullable: false),
                    employee_id = table.Column<int>(type: "int", nullable: false),
                    allocated_wage = table.Column<decimal>(type: "decimal(15,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())"),
                    updated_at = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_wages", x => new { x.booking_id, x.employee_id });
                    table.ForeignKey(
                        name: "FK_booking_wages_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_booking_wages_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_booking_wage_audit_logs_booking_id",
                table: "booking_wage_audit_logs",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_wage_audit_logs_performed_by_user_id",
                table: "booking_wage_audit_logs",
                column: "performed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_wages_employee_id",
                table: "booking_wages",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "idx_bookings_status",
                table: "bookings",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_kol_id",
                table: "bookings",
                column: "kol_id");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_primary_manager_id",
                table: "bookings",
                column: "primary_manager_id");

            migrationBuilder.CreateIndex(
                name: "idx_employees_dept",
                table: "employees",
                column: "department");

            migrationBuilder.CreateIndex(
                name: "IX_employees_manager_id",
                table: "employees",
                column: "manager_id");

            migrationBuilder.CreateIndex(
                name: "UX_employees_email",
                table: "employees",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_employees_user_id",
                table: "employees",
                column: "user_id",
                unique: true,
                filter: "[user_id] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_ideas_status",
                table: "ideas",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_ideas_creator_employee_id",
                table: "ideas",
                column: "creator_employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_ideas_primary_staff_id",
                table: "ideas",
                column: "primary_staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_ideas_reviewer_employee_id",
                table: "ideas",
                column: "reviewer_employee_id");

            migrationBuilder.CreateIndex(
                name: "idx_kols_platform",
                table: "kols",
                column: "platform");

            migrationBuilder.CreateIndex(
                name: "idx_kols_status",
                table: "kols",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_kols_responsible_staff_id",
                table: "kols",
                column: "responsible_staff_id");

            migrationBuilder.CreateIndex(
                name: "idx_audit_logs_created",
                table: "system_audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_audit_logs_login_history",
                table: "system_audit_logs",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true },
                filter: "[module] = 'Tai_Khoan' AND [user_id] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "action_type", "ip_address", "device_info" });

            migrationBuilder.CreateIndex(
                name: "idx_users_role",
                table: "users",
                column: "role");

            migrationBuilder.CreateIndex(
                name: "idx_users_status_role",
                table: "users",
                columns: new[] { "status", "role" })
                .Annotation("SqlServer:Include", new[] { "username", "email" });

            migrationBuilder.CreateIndex(
                name: "UX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS
                (
                    SELECT 1
                    FROM sys.extended_properties
                    WHERE class = 0
                      AND name = N'HanaMediaAdoptedBaseline'
                )
                    THROW 50021, 'Không thể rollback Initial migration trên database đã adopt dữ liệu cũ.', 1;
                """);

            migrationBuilder.DropTable(
                name: "booking_wage_audit_logs");

            migrationBuilder.DropTable(
                name: "booking_wages");

            migrationBuilder.DropTable(
                name: "business_configs");

            migrationBuilder.DropTable(
                name: "ideas");

            migrationBuilder.DropTable(
                name: "system_audit_logs");

            migrationBuilder.DropTable(
                name: "system_configs");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "kols");

            migrationBuilder.DropTable(
                name: "employees");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
