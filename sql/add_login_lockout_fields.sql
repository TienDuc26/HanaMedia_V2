-- ============================================================
-- Module 1 - Brute-force protection
-- File: add_login_lockout_fields.sql
-- Người chạy: tất cả thành viên trong team sau khi pull code
-- Cách chạy: mở SQL Server Management Studio > chọn DB > New Query > paste > Execute
-- ============================================================

-- Kiểm tra field đã tồn tại chưa (tránh chạy 2 lần)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[users]')
      AND name = 'failed_login_attempts'
)
BEGIN
    ALTER TABLE [users]
    ADD [failed_login_attempts] INT NOT NULL CONSTRAINT DF_users_failed_login_attempts DEFAULT 0;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[users]')
      AND name = 'locked_until'
)
BEGIN
    ALTER TABLE [users]
    ADD [locked_until] DATETIME2 NULL;
END
GO

-- Verify
SELECT
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'users'
  AND COLUMN_NAME IN ('failed_login_attempts', 'locked_until');
GO
