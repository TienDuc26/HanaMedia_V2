-- Update all empty/null security_stamp values to valid GUIDs
UPDATE [users]
SET [security_stamp] = LOWER(CONVERT(varchar(36), NEWID()))
WHERE [security_stamp] IS NULL OR [security_stamp] = '';

-- Verify the update
SELECT id, username, security_stamp
FROM [users]
WHERE [security_stamp] IS NULL OR [security_stamp] = '';
