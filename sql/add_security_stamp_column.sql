-- Thêm cột security_stamp vào bảng users
ALTER TABLE [users] ADD [security_stamp] nvarchar(max) NOT NULL DEFAULT '';

-- Verify
SELECT COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'users';
