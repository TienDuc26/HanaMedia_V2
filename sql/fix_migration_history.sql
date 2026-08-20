-- Fix EF Migration History
-- Chạy script này trong SQL Server (SSMS) trên database HanaMedia

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES ('20260820094132_InitialV2Module2', '8.0.8');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES ('20260820104758_add_login_lockout_fields_to_users_table', '8.0.8');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES ('20260820135600_AddSecurityStampColumn', '8.0.8');

-- Verify
SELECT * FROM [__EFMigrationsHistory];
