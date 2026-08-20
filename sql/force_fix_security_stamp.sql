-- Force fix: Check current state and fix all problems
SELECT 'BEFORE UPDATE' as step, COUNT(*) as empty_count
FROM [users]
WHERE [security_stamp] IS NULL OR
      LEN(LTRIM(RTRIM([security_stamp]))) = 0 OR
      TRY_CAST([security_stamp] AS UNIQUEIDENTIFIER) IS NULL;

-- Update all rows
UPDATE [users]
SET [security_stamp] = LOWER(CONVERT(varchar(36), NEWID()));

-- Verify
SELECT 'AFTER UPDATE' as step, COUNT(*) as empty_count
FROM [users]
WHERE [security_stamp] IS NULL OR
      LEN(LTRIM(RTRIM([security_stamp]))) = 0 OR
      TRY_CAST([security_stamp] AS UNIQUEIDENTIFIER) IS NULL;

-- Show sample
SELECT TOP 5 id, username, security_stamp, LEN(security_stamp) as stamp_len
FROM [users];
