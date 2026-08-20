-- =========================================================================
-- MS SQL SERVER (MSSQL) DATABASE SCHEMA - HANAMEDIA
-- =========================================================================
-- FILE LEGACY CHỈ ĐỂ THAM KHẢO, KHÔNG DÙNG ĐỂ DỰNG DATABASE MỚI.
-- EF Core migrations trong thư mục Migrations là nguồn chuẩn duy nhất để đồng bộ
-- cấu trúc database giữa các thành viên.

SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
GO

-- 1. Bảng tài khoản đăng nhập hệ thống
CREATE TABLE users (
    id INT IDENTITY(1,1) PRIMARY KEY,
    username VARCHAR(50) NOT NULL UNIQUE,
    email VARCHAR(100) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    role VARCHAR(20) NOT NULL CONSTRAINT chk_user_role CHECK (role IN ('giam_doc', 'admin_it', 'ql_hcns', 'nv_hcns', 'ql_booking', 'nv_booking', 'ql_y_tuong', 'nv_y_tuong')),
    status VARCHAR(20) NOT NULL DEFAULT 'active' CONSTRAINT chk_user_status CHECK (status IN ('active', 'locked')),
    security_stamp UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    created_at DATETIME DEFAULT GETDATE(),
    updated_at DATETIME DEFAULT GETDATE()
);
GO

-- 2. Bảng hồ sơ nhân viên toàn công ty
CREATE TABLE employees (
    id INT IDENTITY(1,1) PRIMARY KEY,
    user_id INT, -- Có thể NULL nếu nhân sự chưa được cấp tài khoản hệ thống
    full_name NVARCHAR(100) NOT NULL,
    avatar_url VARCHAR(255),
    dob DATE NOT NULL,
    phone VARCHAR(20) NOT NULL,
    email VARCHAR(100) NOT NULL UNIQUE,
    address NVARCHAR(255) NOT NULL,
    joined_date DATE NOT NULL,
    department VARCHAR(20) NOT NULL CONSTRAINT chk_emp_dept CHECK (department IN ('HCNS', 'Booking', 'Y_tuong', 'IT')),
    position NVARCHAR(100) NOT NULL,
    manager_id INT, -- ID nhân viên quản lý trực tiếp
    contract_type VARCHAR(30) NOT NULL CONSTRAINT chk_emp_contract CHECK (contract_type IN ('thu_viec', 'chinh_thuc_1_nam', 'vo_thoi_han')),
    basic_salary DECIMAL(15, 2) NOT NULL, -- Dữ liệu nhạy cảm tài chính
    allowance DECIMAL(15, 2) DEFAULT 0.00,  -- Dữ liệu nhạy cảm tài chính
    status VARCHAR(30) DEFAULT 'thu_viec' CONSTRAINT chk_emp_status CHECK (status IN ('dang_lam_viec', 'thu_viec', 'cho_duyet_nghi', 'ngung_hoat_dong')),
    created_at DATETIME DEFAULT GETDATE(),
    updated_at DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE SET NULL,
    FOREIGN KEY (manager_id) REFERENCES employees(id)
);
GO

-- 3. Bảng cơ sở dữ liệu đối tác KOL/KOC
CREATE TABLE kols (
    id INT IDENTITY(1,1) PRIMARY KEY,
    name NVARCHAR(100) NOT NULL,
    platform VARCHAR(20) NOT NULL CONSTRAINT chk_kol_platform CHECK (platform IN ('TikTok', 'Instagram', 'YouTube', 'Facebook')),
    profile_link VARCHAR(255) NOT NULL,
    followers_count INT NOT NULL,
    engagement_rate DECIMAL(5, 2) NOT NULL, -- Ví dụ: 8.50%
    niche NVARCHAR(100) NOT NULL, -- Chủ đề ví dụ: Beauty, F&B, Vlog
    booking_price DECIMAL(15, 2) NOT NULL,
    location NVARCHAR(100) NOT NULL,
    contact_info NVARCHAR(255) NOT NULL,
    responsible_staff_id INT, -- Nhân viên Booking phụ trách deal
    rating_score TINYINT CONSTRAINT chk_kol_rating CHECK (rating_score BETWEEN 1 AND 5), -- Đánh giá sao
    status VARCHAR(30) DEFAULT 'tiem_nang' CONSTRAINT chk_kol_status CHECK (status IN ('tiem_nang', 'da_lien_he', 'dang_deal', 'da_chot', 'dang_chay', 'hoan_thanh')),
    created_at DATETIME DEFAULT GETDATE(),
    updated_at DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (responsible_staff_id) REFERENCES employees(id) ON DELETE SET NULL
);
GO

-- 4. Bảng quản lý chiến dịch / Booking chính
CREATE TABLE bookings (
    id INT IDENTITY(1,1) PRIMARY KEY,
    client_name NVARCHAR(100) NOT NULL,
    campaign_name NVARCHAR(100) NOT NULL,
    kol_id INT,
    job_description NVARCHAR(MAX),
    deadline DATE NOT NULL,
    posting_date DATE,
    booking_price DECIMAL(15, 2) NOT NULL, -- Giá trị hợp đồng nhận từ Client
    actual_cost DECIMAL(15, 2) NOT NULL,    -- Phí thực tế deal với KOL + dịch vụ
    primary_manager_id INT, -- Quản lý Booking hoặc nhân viên phụ trách chính
    status VARCHAR(30) DEFAULT 'dang_cho' CONSTRAINT chk_booking_status CHECK (status IN ('dang_cho', 'thuong_luong', 'da_chot', 'dang_trien_khai', 'hoan_thanh', 'huy')),
    contract_file_url VARCHAR(255),
    quotation_file_url VARCHAR(255),
    post_link VARCHAR(255),
    notes NVARCHAR(MAX),
    created_at DATETIME DEFAULT GETDATE(),
    updated_at DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (kol_id) REFERENCES kols(id) ON DELETE SET NULL,
    FOREIGN KEY (primary_manager_id) REFERENCES employees(id)
);
GO

-- 5. Bảng phân bổ thù lao chi tiết cho nhân viên
CREATE TABLE booking_wages (
    booking_id INT,
    employee_id INT,
    allocated_wage DECIMAL(15, 2) NOT NULL,
    created_at DATETIME DEFAULT GETDATE(),
    updated_at DATETIME DEFAULT GETDATE(),
    PRIMARY KEY (booking_id, employee_id),
    FOREIGN KEY (booking_id) REFERENCES bookings(id) ON DELETE CASCADE,
    FOREIGN KEY (employee_id) REFERENCES employees(id) ON DELETE CASCADE
);
GO

-- 6. Bảng nhật ký thay đổi thù lao (Audit logs phục vụ đối soát)
CREATE TABLE booking_wage_audit_logs (
    id INT IDENTITY(1,1) PRIMARY KEY,
    booking_id INT,
    performed_by_user_id INT,
    log_detail NVARCHAR(255) NOT NULL,
    created_at DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (booking_id) REFERENCES bookings(id) ON DELETE CASCADE,
    FOREIGN KEY (performed_by_user_id) REFERENCES users(id) ON DELETE SET NULL
);
GO

-- 7. Bảng quản lý kho thư viện ý tưởng và kịch bản sáng tạo
CREATE TABLE ideas (
    id INT IDENTITY(1,1) PRIMARY KEY,
    title NVARCHAR(150) NOT NULL,
    creator_employee_id INT,
    client_name NVARCHAR(100) NOT NULL,
    campaign_name NVARCHAR(100) NOT NULL,
    industry NVARCHAR(100) NOT NULL, -- Ngành hàng (F&B, Mỹ phẩm...)
    category VARCHAR(30) NOT NULL CONSTRAINT chk_idea_cat CHECK (category IN ('trend', 'viral', 'da_trien_khai', 'chua_su_dung')),
    insight NVARCHAR(MAX),
    concept NVARCHAR(MAX),
    content_details NVARCHAR(MAX),
    reference_link VARCHAR(255),
    moodboard_desc NVARCHAR(MAX),
    script_text NVARCHAR(MAX),
    deadline DATE NOT NULL,
    primary_staff_id INT, -- Nhân sự phòng Ý tưởng được giao viết chính
    reviewer_employee_id INT, -- Người duyệt (Quản lý Ý tưởng hoặc Giám đốc)
    status VARCHAR(30) DEFAULT 'y_tuong' CONSTRAINT chk_idea_status CHECK (status IN ('y_tuong', 'review', 'need_revision', 'approved', 'done')),
    feedback_comment NVARCHAR(MAX),
    created_at DATETIME DEFAULT GETDATE(),
    updated_at DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (creator_employee_id) REFERENCES employees(id),
    FOREIGN KEY (primary_staff_id) REFERENCES employees(id),
    FOREIGN KEY (reviewer_employee_id) REFERENCES employees(id)
);
GO

-- 8. Bảng nhật ký hoạt động hệ thống (System Audit Log cho AdminIT & Giám đốc)
CREATE TABLE system_audit_logs (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    user_id INT,
    action_type VARCHAR(50) NOT NULL, -- login, logout, create, edit, delete, approve, change_role
    module VARCHAR(30) NOT NULL CONSTRAINT chk_log_module CHECK (module IN ('Nhan_Su', 'Booking', 'Y_Tuong', 'Tai_Khoan', 'Cau_Hinh')),
    log_detail NVARCHAR(MAX) NOT NULL,
    ip_address VARCHAR(45) NOT NULL,
    device_info VARCHAR(255),
    created_at DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE SET NULL
);
GO

-- 9. Bảng cấu hình kỹ thuật hệ thống (AdminIT quản trị)
CREATE TABLE system_configs (
    config_key VARCHAR(100) PRIMARY KEY,
    config_value VARCHAR(255) NOT NULL,
    description NVARCHAR(255),
    updated_at DATETIME DEFAULT GETDATE()
);
GO

-- 10. Bảng cấu hình chính sách nghiệp vụ (Giám đốc quản trị)
CREATE TABLE business_configs (
    config_key VARCHAR(100) PRIMARY KEY,
    config_value VARCHAR(255) NOT NULL,
    description NVARCHAR(255),
    updated_at DATETIME DEFAULT GETDATE()
);
GO

-- =========================================================================
-- OPTIMIZATION INDEXES
-- =========================================================================
CREATE INDEX idx_users_role ON users(role);
CREATE INDEX idx_users_status_role ON users(status, role) INCLUDE(username, email);
CREATE UNIQUE INDEX UX_employees_user_id ON employees(user_id) WHERE user_id IS NOT NULL;
CREATE INDEX idx_employees_dept ON employees(department);
CREATE INDEX idx_kols_platform ON kols(platform);
CREATE INDEX idx_kols_status ON kols(status);
CREATE INDEX idx_bookings_status ON bookings(status);
CREATE INDEX idx_ideas_status ON ideas(status);
CREATE INDEX idx_audit_logs_created ON system_audit_logs(created_at);
CREATE INDEX idx_audit_logs_login_history
    ON system_audit_logs(user_id, created_at DESC)
    INCLUDE(action_type, ip_address, device_info)
    WHERE module = 'Tai_Khoan' AND user_id IS NOT NULL;
GO

-- =========================================================================
-- INSERT TEST USERS (Password for all: password123)
-- =========================================================================
-- password_hash is SHA256 of 'password123': ef92b778bafe421e48a550b0915f1116abd124136458f00dbd8551c69a7a93bc
-- =========================================================================
INSERT INTO users (username, email, password_hash, role, status) VALUES
('giam_doc', 'giamdoc@hanamedia.com', 'ef92b778bafe421e48a550b0915f1116abd124136458f00dbd8551c69a7a93bc', 'giam_doc', 'active'),
('admin_it', 'adminit@hanamedia.com', 'ef92b778bafe421e48a550b0915f1116abd124136458f00dbd8551c69a7a93bc', 'admin_it', 'active'),
('ql_hcns', 'qlhcns@hanamedia.com', 'ef92b778bafe421e48a550b0915f1116abd124136458f00dbd8551c69a7a93bc', 'ql_hcns', 'active'),
('nv_hcns', 'nvhcns@hanamedia.com', 'ef92b778bafe421e48a550b0915f1116abd124136458f00dbd8551c69a7a93bc', 'nv_hcns', 'active'),
('ql_booking', 'qlbooking@hanamedia.com', 'ef92b778bafe421e48a550b0915f1116abd124136458f00dbd8551c69a7a93bc', 'ql_booking', 'active'),
('nv_booking', 'nvbooking@hanamedia.com', 'ef92b778bafe421e48a550b0915f1116abd124136458f00dbd8551c69a7a93bc', 'nv_booking', 'active'),
('ql_y_tuong', 'qlytuong@hanamedia.com', 'ef92b778bafe421e48a550b0915f1116abd124136458f00dbd8551c69a7a93bc', 'ql_y_tuong', 'active'),
('nv_y_tuong', 'nvytuong@hanamedia.com', 'ef92b778bafe421e48a550b0915f1116abd124136458f00dbd8551c69a7a93bc', 'nv_y_tuong', 'active');
GO
