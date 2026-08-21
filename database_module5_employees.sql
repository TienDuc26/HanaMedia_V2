-- ============================================================
-- Module 5: Quản lý nhân sự (Hồ sơ nhân viên)
-- File: migration_module5_employees.sql
-- Mục đích: Script idempotent (chạy nhiều lần vẫn OK)
-- Áp dụng: SQL Server 2019+
-- Cách chạy: mở SQL Server Management Studio, chạy file này
-- HOẶC: dotnet ef database update (đã có migration InitialV2Module2)
-- ============================================================

SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

-- 1) Bảng employees (chỉ tạo nếu chưa có)
IF OBJECT_ID(N'[dbo].[employees]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[employees](
        [id]                    INT IDENTITY(1,1) NOT NULL,
        [user_id]               INT NULL,
        [full_name]             NVARCHAR(100) NOT NULL,
        [avatar_url]            NVARCHAR(255) NULL,
        [dob]                   DATE NOT NULL,
        [phone]                 VARCHAR(20) NOT NULL,
        [email]                 VARCHAR(100) NOT NULL,
        [address]               NVARCHAR(255) NOT NULL,
        [joined_date]           DATE NOT NULL,
        [department]            VARCHAR(20) NOT NULL,
        [position]              NVARCHAR(100) NOT NULL,
        [manager_id]            INT NULL,
        [contract_type]         VARCHAR(30) NOT NULL,
        [basic_salary]          DECIMAL(15,2) NOT NULL,
        [allowance]             DECIMAL(15,2) NULL,
        [status]                VARCHAR(30) NOT NULL DEFAULT 'thu_viec',
        [created_at]            DATETIME NOT NULL DEFAULT (GETDATE()),
        [updated_at]            DATETIME NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_employees] PRIMARY KEY CLUSTERED ([id] ASC),
        CONSTRAINT [chk_emp_dept] CHECK ([department] IN ('HCNS','Booking','Y_tuong','IT')),
        CONSTRAINT [chk_emp_contract] CHECK ([contract_type] IN ('thu_viec','chinh_thuc_1_nam','vo_thoi_han')),
        CONSTRAINT [chk_emp_status] CHECK ([status] IN ('dang_lam_viec','thu_viec','cho_duyet_nghi','ngung_hoat_dong','tam_ngung','da_nghi'))
    );
    PRINT '[OK] Created table employees';
END
ELSE
BEGIN
    PRINT '[SKIP] Table employees already exists';
END
GO

-- 2) Unique index email
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_employees_email' AND object_id = OBJECT_ID('employees'))
BEGIN
    CREATE UNIQUE INDEX [UX_employees_email] ON [dbo].[employees]([email] ASC);
    PRINT '[OK] Created index UX_employees_email';
END
GO

-- 3) Unique index user_id (filter null)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_employees_user_id' AND object_id = OBJECT_ID('employees'))
BEGIN
    CREATE UNIQUE INDEX [UX_employees_user_id] ON [dbo].[employees]([user_id] ASC) WHERE [user_id] IS NOT NULL;
    PRINT '[OK] Created index UX_employees_user_id';
END
GO

-- 4) Index department
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_employees_dept' AND object_id = OBJECT_ID('employees'))
BEGIN
    CREATE INDEX [idx_employees_dept] ON [dbo].[employees]([department] ASC);
    PRINT '[OK] Created index idx_employees_dept';
END
GO

-- 5) Foreign key manager (self-reference)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_employees_employees_manager_id')
BEGIN
    ALTER TABLE [dbo].[employees] WITH CHECK
    ADD CONSTRAINT [FK_employees_employees_manager_id] FOREIGN KEY([manager_id])
    REFERENCES [dbo].[employees]([id]);
    PRINT '[OK] Created FK_employees_employees_manager_id';
END
GO

-- 6) Foreign key user_id -> users.id
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_employees_users_user_id')
BEGIN
    ALTER TABLE [dbo].[employees] WITH CHECK
    ADD CONSTRAINT [FK_employees_users_user_id] FOREIGN KEY([user_id])
    REFERENCES [dbo].[users]([id])
    ON DELETE SET NULL;
    PRINT '[OK] Created FK_employees_users_user_id';
END
GO

-- 7) Foreign key department -> departments.code (theo code, không phải id)
-- Lưu ý: department là VARCHAR(20) tham chiếu departments.code
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_employees_departments_department')
BEGIN
    ALTER TABLE [dbo].[employees] WITH CHECK
    ADD CONSTRAINT [FK_employees_departments_department] FOREIGN KEY([department])
    REFERENCES [dbo].[departments]([code]);
    PRINT '[OK] Created FK_employees_departments_department';
END
GO

-- 8) Nếu bảng employees đã có nhưng chưa có field status đầy đủ 3 giá trị mới thì mở rộng check
-- (bỏ qua nếu không thể drop check constraint)
BEGIN TRY
    DECLARE @sql NVARCHAR(MAX);
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'chk_emp_status' AND parent_object_id = OBJECT_ID('employees'))
    BEGIN
        SET @sql = 'ALTER TABLE [dbo].[employees] DROP CONSTRAINT [chk_emp_status]';
        EXEC sp_executesql @sql;
        SET @sql = 'ALTER TABLE [dbo].[employees] ADD CONSTRAINT [chk_emp_status] CHECK ([status] IN (''dang_lam_viec'',''thu_viec'',''cho_duyet_nghi'',''ngung_hoat_dong'',''tam_ngung'',''da_nghi''))';
        EXEC sp_executesql @sql;
        PRINT '[OK] Updated chk_emp_status to support tam_ngung, da_nghi';
    END
END TRY
BEGIN CATCH
    PRINT '[INFO] Could not update chk_emp_status: ' + ERROR_MESSAGE();
END CATCH
GO

-- 9) Thư mục uploads/avatars (tạo folder cho wwwroot - ghi nhận, cần tạo thủ công trên filesystem)
-- File ảnh được upload qua endpoint: POST /ManageHuman/Employee/ApiUploadAvatar/{id}
-- File sẽ được lưu ở wwwroot/uploads/avatars/

COMMIT;

PRINT '';
PRINT '=== Module 5 migration script DONE ===';
GO
