SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @migrationId NVARCHAR(150) = N'20260820094132_InitialV2Module2';
DECLARE @productVersion NVARCHAR(32) = N'8.0.8';
DECLARE @markerName SYSNAME = N'HanaMediaAdoptedBaseline';
DECLARE @errorMessage NVARCHAR(2048);
DECLARE @sql NVARCHAR(MAX);

BEGIN TRY
    BEGIN TRANSACTION;

    IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
        THROW 50001, 'Không được chạy adoption script trong system database.', 1;

    DECLARE @lockResult INT;
    EXEC @lockResult = sys.sp_getapplock
        @Resource = N'HanaMedia:AdoptV2Module2Migrations',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;

    IF @lockResult < 0
        THROW 50001, 'Không thể lấy khóa adoption database.', 1;

    DECLARE @markerValue NVARCHAR(4000);
    SELECT @markerValue = CONVERT(NVARCHAR(4000), value)
    FROM sys.extended_properties
    WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = @markerName;

    -- Tạo history table trong transaction trước khi tham chiếu tĩnh tới nó.
    -- Nếu preflight thất bại, transaction sẽ rollback cả bảng này.
    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.__EFMigrationsHistory
        (
            MigrationId NVARCHAR(150) NOT NULL,
            ProductVersion NVARCHAR(32) NOT NULL,
            CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY (MigrationId)
        );
    END
    ELSE IF COL_LENGTH(N'dbo.__EFMigrationsHistory', N'MigrationId') IS NULL
         OR COL_LENGTH(N'dbo.__EFMigrationsHistory', N'ProductVersion') IS NULL
        THROW 50002, '__EFMigrationsHistory có schema không tương thích.', 1;

    IF @markerValue IS NOT NULL AND @markerValue <> @migrationId
        THROW 50002, 'Database có adoption marker không tương thích.', 1;

    IF @markerValue IS NULL
       AND EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory)
        THROW 50002, 'Database đã có migration history nhưng không có HanaMedia adoption marker.', 1;

    IF @markerValue = @migrationId
       AND NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = @migrationId)
        THROW 50002, 'Adoption marker và migration history không đồng nhất.', 1;

    IF @markerValue = @migrationId
       AND EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId <> @migrationId)
        THROW 50002, 'Không chạy lại adoption script sau khi đã có migration mới hơn.', 1;

    DECLARE @requiredTables TABLE (table_name SYSNAME NOT NULL PRIMARY KEY);
    INSERT INTO @requiredTables (table_name)
    VALUES
        (N'users'), (N'employees'), (N'kols'), (N'bookings'),
        (N'booking_wages'), (N'booking_wage_audit_logs'), (N'ideas'),
        (N'system_audit_logs'), (N'system_configs'), (N'business_configs');

    SELECT TOP (1) @errorMessage = N'Thiếu bảng dbo.' + table_name + N'.'
    FROM @requiredTables
    WHERE OBJECT_ID(N'dbo.' + table_name, N'U') IS NULL
    ORDER BY table_name;

    IF @errorMessage IS NOT NULL
        THROW 50003, @errorMessage, 1;

    DECLARE @requiredColumns TABLE
    (
        table_name SYSNAME NOT NULL,
        column_name SYSNAME NOT NULL,
        type_name SYSNAME NOT NULL,
        max_length SMALLINT NOT NULL,
        is_nullable TINYINT NOT NULL,
        PRIMARY KEY (table_name, column_name)
    );

    -- is_nullable = 2 cho phép NULL hoặc NOT NULL trước khi normalize.
    INSERT INTO @requiredColumns (table_name, column_name, type_name, max_length, is_nullable)
    VALUES
        (N'users', N'id', N'int', 4, 0),
        (N'users', N'username', N'varchar', 50, 0),
        (N'users', N'email', N'varchar', 100, 0),
        (N'users', N'password_hash', N'varchar', 255, 0),
        (N'users', N'role', N'varchar', 20, 0),
        (N'users', N'status', N'varchar', 20, 2),
        (N'employees', N'id', N'int', 4, 0),
        (N'employees', N'user_id', N'int', 4, 1),
        (N'employees', N'email', N'varchar', 100, 0),
        (N'system_audit_logs', N'id', N'bigint', 8, 0),
        (N'system_audit_logs', N'user_id', N'int', 4, 1),
        (N'system_audit_logs', N'action_type', N'varchar', 50, 0),
        (N'system_audit_logs', N'module', N'varchar', 30, 0),
        (N'system_audit_logs', N'log_detail', N'nvarchar', -1, 0),
        (N'system_audit_logs', N'ip_address', N'varchar', 45, 0),
        (N'system_audit_logs', N'device_info', N'varchar', 255, 1),
        (N'system_audit_logs', N'created_at', N'datetime', 8, 1);

    SET @errorMessage = NULL;
    SELECT TOP (1)
        @errorMessage = CONCAT(N'Cột Module 2 không khớp: dbo.', expected.table_name, N'.', expected.column_name, N'.')
    FROM @requiredColumns AS expected
    LEFT JOIN sys.tables AS table_object
        ON table_object.name = expected.table_name AND table_object.schema_id = SCHEMA_ID(N'dbo')
    LEFT JOIN sys.columns AS actual
        ON actual.object_id = table_object.object_id AND actual.name = expected.column_name
    WHERE actual.column_id IS NULL
       OR TYPE_NAME(actual.user_type_id) <> expected.type_name
       OR actual.max_length <> expected.max_length
       OR (expected.is_nullable IN (0, 1) AND actual.is_nullable <> expected.is_nullable)
    ORDER BY expected.table_name, expected.column_name;

    IF @errorMessage IS NOT NULL
        THROW 50004, @errorMessage, 1;

    IF COL_LENGTH(N'dbo.users', N'security_stamp') IS NOT NULL
       AND NOT EXISTS
       (
           SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.users')
             AND name = N'security_stamp'
             AND TYPE_NAME(user_type_id) = N'uniqueidentifier'
             AND max_length = 16
       )
        THROW 50004, 'dbo.users.security_stamp tồn tại nhưng sai kiểu dữ liệu.', 1;

    IF EXISTS (SELECT 1 FROM dbo.users WHERE role NOT IN ('giam_doc', 'admin_it', 'ql_hcns', 'nv_hcns', 'ql_booking', 'nv_booking', 'ql_y_tuong', 'nv_y_tuong'))
        THROW 50005, 'dbo.users có role ngoài danh sách hỗ trợ.', 1;
    IF EXISTS (SELECT 1 FROM dbo.users WHERE status IS NOT NULL AND status NOT IN ('active', 'locked'))
        THROW 50005, 'dbo.users có status ngoài danh sách hỗ trợ.', 1;
    IF EXISTS (SELECT username FROM dbo.users GROUP BY username HAVING COUNT(*) > 1)
        THROW 50005, 'Có username trùng.', 1;
    IF EXISTS (SELECT email FROM dbo.users GROUP BY email HAVING COUNT(*) > 1)
        THROW 50005, 'Có email tài khoản trùng.', 1;
    IF EXISTS (SELECT email FROM dbo.employees GROUP BY email HAVING COUNT(*) > 1)
        THROW 50005, 'Có email nhân viên trùng.', 1;
    IF EXISTS (SELECT user_id FROM dbo.employees WHERE user_id IS NOT NULL GROUP BY user_id HAVING COUNT(*) > 1)
        THROW 50005, 'Một tài khoản đang liên kết nhiều nhân viên.', 1;

    UPDATE dbo.users SET status = 'locked' WHERE status IS NULL;

    IF EXISTS
    (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.users') AND name = N'status' AND is_nullable = 1
    )
    BEGIN
        IF EXISTS
        (
            SELECT 1 FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'dbo.users')
              AND name = N'idx_users_status_role'
              AND is_primary_key = 0
              AND is_unique_constraint = 0
        )
            DROP INDEX idx_users_status_role ON dbo.users;

        ALTER TABLE dbo.users ALTER COLUMN status VARCHAR(20) NOT NULL;
    END;

    IF COL_LENGTH(N'dbo.users', N'security_stamp') IS NULL
    BEGIN
        ALTER TABLE dbo.users
        ADD security_stamp UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_users_security_stamp DEFAULT (NEWID()) WITH VALUES;
    END
    ELSE
    BEGIN
        IF EXISTS
        (
            SELECT 1 FROM sys.columns
            WHERE object_id = OBJECT_ID(N'dbo.users') AND name = N'security_stamp' AND is_nullable = 1
        )
        BEGIN
            SET @sql = N'UPDATE dbo.users SET security_stamp = NEWID() WHERE security_stamp IS NULL;';
            EXEC sys.sp_executesql @sql;
            SET @sql = N'ALTER TABLE dbo.users ALTER COLUMN security_stamp UNIQUEIDENTIFIER NOT NULL;';
            EXEC sys.sp_executesql @sql;
        END;

        SET @sql = N'UPDATE dbo.users SET security_stamp = NEWID()
            WHERE security_stamp = ''00000000-0000-0000-0000-000000000000'';';
        EXEC sys.sp_executesql @sql;
    END;

    DECLARE @defaultName SYSNAME;
    DECLARE @defaultDefinition NVARCHAR(MAX);

    SELECT @defaultName = default_object.name, @defaultDefinition = default_object.definition
    FROM sys.columns AS column_object
    LEFT JOIN sys.default_constraints AS default_object
        ON default_object.parent_object_id = column_object.object_id
       AND default_object.parent_column_id = column_object.column_id
    WHERE column_object.object_id = OBJECT_ID(N'dbo.users') AND column_object.name = N'status';

    IF @defaultName IS NULL
       OR LOWER(REPLACE(REPLACE(REPLACE(@defaultDefinition, N'(', N''), N')', N''), N' ', N'')) <> N'''active'''
    BEGIN
        IF @defaultName IS NOT NULL
        BEGIN
            SET @sql = N'ALTER TABLE dbo.users DROP CONSTRAINT ' + QUOTENAME(@defaultName) + N';';
            EXEC sys.sp_executesql @sql;
        END;
        ALTER TABLE dbo.users ADD CONSTRAINT DF_users_status DEFAULT ('active') FOR status;
    END;

    SET @defaultName = NULL;
    SET @defaultDefinition = NULL;
    SELECT @defaultName = default_object.name, @defaultDefinition = default_object.definition
    FROM sys.columns AS column_object
    LEFT JOIN sys.default_constraints AS default_object
        ON default_object.parent_object_id = column_object.object_id
       AND default_object.parent_column_id = column_object.column_id
    WHERE column_object.object_id = OBJECT_ID(N'dbo.users') AND column_object.name = N'security_stamp';

    IF @defaultName IS NULL
       OR LOWER(REPLACE(REPLACE(REPLACE(@defaultDefinition, N'(', N''), N')', N''), N' ', N'')) <> N'newid'
    BEGIN
        IF @defaultName IS NOT NULL
        BEGIN
            SET @sql = N'ALTER TABLE dbo.users DROP CONSTRAINT ' + QUOTENAME(@defaultName) + N';';
            EXEC sys.sp_executesql @sql;
        END;
        SET @sql = N'ALTER TABLE dbo.users ADD CONSTRAINT DF_users_security_stamp DEFAULT (NEWID()) FOR security_stamp;';
        EXEC sys.sp_executesql @sql;
    END;

    DECLARE @expectedPrimaryKeys TABLE
    (
        table_name SYSNAME NOT NULL PRIMARY KEY,
        column_signature NVARCHAR(400) NOT NULL,
        canonical_name SYSNAME NOT NULL
    );

    INSERT INTO @expectedPrimaryKeys (table_name, column_signature, canonical_name)
    VALUES
        (N'business_configs', N'config_key', N'PK_business_configs'),
        (N'system_configs', N'config_key', N'PK_system_configs'),
        (N'users', N'id', N'PK_users'),
        (N'employees', N'id', N'PK_employees'),
        (N'system_audit_logs', N'id', N'PK_system_audit_logs'),
        (N'ideas', N'id', N'PK_ideas'),
        (N'kols', N'id', N'PK_kols'),
        (N'bookings', N'id', N'PK_bookings'),
        (N'booking_wage_audit_logs', N'id', N'PK_booking_wage_audit_logs'),
        (N'booking_wages', N'booking_id,employee_id', N'PK_booking_wages');

    DECLARE @tableName SYSNAME;
    DECLARE @columnSignature NVARCHAR(400);
    DECLARE @canonicalName SYSNAME;
    DECLARE @actualName SYSNAME;
    DECLARE @matchCount INT;
    DECLARE @qualifiedName NVARCHAR(776);

    DECLARE pk_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT table_name, column_signature, canonical_name FROM @expectedPrimaryKeys ORDER BY table_name;
    OPEN pk_cursor;
    FETCH NEXT FROM pk_cursor INTO @tableName, @columnSignature, @canonicalName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @actualName = NULL;
        SET @matchCount = 0;

        SELECT @matchCount = COUNT(*), @actualName = MIN(candidate.constraint_name)
        FROM
        (
            SELECT key_object.name AS constraint_name,
                   STRING_AGG(CONVERT(NVARCHAR(MAX), column_object.name), N',')
                       WITHIN GROUP (ORDER BY index_column.key_ordinal) AS key_columns
            FROM sys.key_constraints AS key_object
            INNER JOIN sys.indexes AS index_object
                ON index_object.object_id = key_object.parent_object_id
               AND index_object.index_id = key_object.unique_index_id
            INNER JOIN sys.index_columns AS index_column
                ON index_column.object_id = index_object.object_id
               AND index_column.index_id = index_object.index_id
               AND index_column.key_ordinal > 0
            INNER JOIN sys.columns AS column_object
                ON column_object.object_id = index_column.object_id
               AND column_object.column_id = index_column.column_id
            WHERE key_object.parent_object_id = OBJECT_ID(N'dbo.' + @tableName)
              AND key_object.type = N'PK'
              AND index_object.type = 1
              AND index_object.is_disabled = 0
            GROUP BY key_object.name
        ) AS candidate
        WHERE candidate.key_columns = @columnSignature;

        IF @matchCount <> 1
        BEGIN
            SET @errorMessage = CONCAT(N'Primary key không khớp: dbo.', @tableName, N'.');
            THROW 50006, @errorMessage, 1;
        END;

        IF @actualName <> @canonicalName
        BEGIN
            IF OBJECT_ID(N'dbo.' + @canonicalName) IS NOT NULL
            BEGIN
                SET @errorMessage = N'Primary key canonical bị trùng tên: ' + @canonicalName + N'.';
                THROW 50006, @errorMessage, 1;
            END;
            SET @qualifiedName = QUOTENAME(N'dbo') + N'.' + QUOTENAME(@actualName);
            EXEC sys.sp_rename @objname = @qualifiedName, @newname = @canonicalName, @objtype = N'OBJECT';
        END;

        FETCH NEXT FROM pk_cursor INTO @tableName, @columnSignature, @canonicalName;
    END;
    CLOSE pk_cursor;
    DEALLOCATE pk_cursor;

    DECLARE @expectedForeignKeys TABLE
    (
        parent_table SYSNAME NOT NULL,
        parent_columns NVARCHAR(400) NOT NULL,
        referenced_table SYSNAME NOT NULL,
        referenced_columns NVARCHAR(400) NOT NULL,
        delete_action TINYINT NOT NULL,
        canonical_name SYSNAME NOT NULL PRIMARY KEY
    );

    INSERT INTO @expectedForeignKeys
        (parent_table, parent_columns, referenced_table, referenced_columns, delete_action, canonical_name)
    VALUES
        (N'employees', N'manager_id', N'employees', N'id', 0, N'FK_employees_employees_manager_id'),
        (N'employees', N'user_id', N'users', N'id', 2, N'FK_employees_users_user_id'),
        (N'system_audit_logs', N'user_id', N'users', N'id', 2, N'FK_system_audit_logs_users_user_id'),
        (N'ideas', N'creator_employee_id', N'employees', N'id', 0, N'FK_ideas_employees_creator_employee_id'),
        (N'ideas', N'primary_staff_id', N'employees', N'id', 0, N'FK_ideas_employees_primary_staff_id'),
        (N'ideas', N'reviewer_employee_id', N'employees', N'id', 0, N'FK_ideas_employees_reviewer_employee_id'),
        (N'kols', N'responsible_staff_id', N'employees', N'id', 2, N'FK_kols_employees_responsible_staff_id'),
        (N'bookings', N'kol_id', N'kols', N'id', 2, N'FK_bookings_kols_kol_id'),
        (N'bookings', N'primary_manager_id', N'employees', N'id', 0, N'FK_bookings_employees_primary_manager_id'),
        (N'booking_wage_audit_logs', N'booking_id', N'bookings', N'id', 1, N'FK_booking_wage_audit_logs_bookings_booking_id'),
        (N'booking_wage_audit_logs', N'performed_by_user_id', N'users', N'id', 2, N'FK_booking_wage_audit_logs_users_performed_by_user_id'),
        (N'booking_wages', N'booking_id', N'bookings', N'id', 1, N'FK_booking_wages_bookings_booking_id'),
        (N'booking_wages', N'employee_id', N'employees', N'id', 1, N'FK_booking_wages_employees_employee_id');

    DECLARE @parentTable SYSNAME;
    DECLARE @parentColumns NVARCHAR(400);
    DECLARE @referencedTable SYSNAME;
    DECLARE @referencedColumns NVARCHAR(400);
    DECLARE @deleteAction TINYINT;

    DECLARE fk_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT parent_table, parent_columns, referenced_table, referenced_columns, delete_action, canonical_name
        FROM @expectedForeignKeys ORDER BY parent_table, canonical_name;
    OPEN fk_cursor;
    FETCH NEXT FROM fk_cursor
        INTO @parentTable, @parentColumns, @referencedTable, @referencedColumns, @deleteAction, @canonicalName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @actualName = NULL;
        SET @matchCount = 0;

        SELECT @matchCount = COUNT(*), @actualName = MIN(candidate.constraint_name)
        FROM
        (
            SELECT foreign_key_object.name AS constraint_name,
                   foreign_key_object.delete_referential_action,
                   foreign_key_object.update_referential_action,
                   foreign_key_object.is_disabled,
                   STRING_AGG(CONVERT(NVARCHAR(MAX), parent_column.name), N',')
                       WITHIN GROUP (ORDER BY foreign_key_column.constraint_column_id) AS actual_parent_columns,
                   STRING_AGG(CONVERT(NVARCHAR(MAX), referenced_column.name), N',')
                       WITHIN GROUP (ORDER BY foreign_key_column.constraint_column_id) AS actual_referenced_columns
            FROM sys.foreign_keys AS foreign_key_object
            INNER JOIN sys.foreign_key_columns AS foreign_key_column
                ON foreign_key_column.constraint_object_id = foreign_key_object.object_id
            INNER JOIN sys.columns AS parent_column
                ON parent_column.object_id = foreign_key_column.parent_object_id
               AND parent_column.column_id = foreign_key_column.parent_column_id
            INNER JOIN sys.columns AS referenced_column
                ON referenced_column.object_id = foreign_key_column.referenced_object_id
               AND referenced_column.column_id = foreign_key_column.referenced_column_id
            WHERE foreign_key_object.parent_object_id = OBJECT_ID(N'dbo.' + @parentTable)
              AND foreign_key_object.referenced_object_id = OBJECT_ID(N'dbo.' + @referencedTable)
            GROUP BY foreign_key_object.name, foreign_key_object.delete_referential_action,
                     foreign_key_object.update_referential_action, foreign_key_object.is_disabled
        ) AS candidate
        WHERE candidate.actual_parent_columns = @parentColumns
          AND candidate.actual_referenced_columns = @referencedColumns
          AND candidate.delete_referential_action = @deleteAction
          AND candidate.update_referential_action = 0
          AND candidate.is_disabled = 0;

        IF @matchCount <> 1
        BEGIN
            SET @errorMessage = CONCAT(N'Foreign key không khớp: dbo.', @parentTable, N'(', @parentColumns, N').');
            THROW 50007, @errorMessage, 1;
        END;

        SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@parentTable)
            + N' WITH CHECK CHECK CONSTRAINT ' + QUOTENAME(@actualName) + N';';
        EXEC sys.sp_executesql @sql;

        IF @actualName <> @canonicalName
        BEGIN
            IF OBJECT_ID(N'dbo.' + @canonicalName) IS NOT NULL
            BEGIN
                SET @errorMessage = N'Foreign key canonical bị trùng tên: ' + @canonicalName + N'.';
                THROW 50007, @errorMessage, 1;
            END;
            SET @qualifiedName = QUOTENAME(N'dbo') + N'.' + QUOTENAME(@actualName);
            EXEC sys.sp_rename @objname = @qualifiedName, @newname = @canonicalName, @objtype = N'OBJECT';
        END;

        FETCH NEXT FROM fk_cursor
            INTO @parentTable, @parentColumns, @referencedTable, @referencedColumns, @deleteAction, @canonicalName;
    END;
    CLOSE fk_cursor;
    DEALLOCATE fk_cursor;

    DECLARE @expectedUniqueIndexes TABLE
    (
        table_name SYSNAME NOT NULL,
        column_name SYSNAME NOT NULL,
        canonical_name SYSNAME NOT NULL PRIMARY KEY,
        normalized_filter NVARCHAR(400) NOT NULL,
        create_sql NVARCHAR(MAX) NOT NULL
    );

    INSERT INTO @expectedUniqueIndexes
        (table_name, column_name, canonical_name, normalized_filter, create_sql)
    VALUES
        (N'employees', N'email', N'UX_employees_email', N'', N'CREATE UNIQUE INDEX UX_employees_email ON dbo.employees(email);'),
        (N'employees', N'user_id', N'UX_employees_user_id', N'user_idisnotnull', N'CREATE UNIQUE INDEX UX_employees_user_id ON dbo.employees(user_id) WHERE user_id IS NOT NULL;'),
        (N'users', N'email', N'UX_users_email', N'', N'CREATE UNIQUE INDEX UX_users_email ON dbo.users(email);'),
        (N'users', N'username', N'UX_users_username', N'', N'CREATE UNIQUE INDEX UX_users_username ON dbo.users(username);');

    DECLARE @columnName SYSNAME;
    DECLARE @normalizedFilter NVARCHAR(400);
    DECLARE @createSql NVARCHAR(MAX);
    DECLARE @canonicalCorrect BIT;
    DECLARE @legacyName SYSNAME;
    DECLARE @isUniqueConstraint BIT;

    DECLARE ux_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT table_name, column_name, canonical_name, normalized_filter, create_sql
        FROM @expectedUniqueIndexes ORDER BY table_name, column_name;
    OPEN ux_cursor;
    FETCH NEXT FROM ux_cursor
        INTO @tableName, @columnName, @canonicalName, @normalizedFilter, @createSql;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @canonicalCorrect = 0;

        IF EXISTS
        (
            SELECT 1
            FROM sys.indexes AS index_object
            INNER JOIN sys.index_columns AS index_column
                ON index_column.object_id = index_object.object_id
               AND index_column.index_id = index_object.index_id
               AND index_column.key_ordinal = 1
            INNER JOIN sys.columns AS indexed_column
                ON indexed_column.object_id = index_column.object_id
               AND indexed_column.column_id = index_column.column_id
            WHERE index_object.object_id = OBJECT_ID(N'dbo.' + @tableName)
              AND index_object.name = @canonicalName
              AND index_object.is_unique = 1
              AND index_object.is_unique_constraint = 0
              AND index_object.is_primary_key = 0
              AND index_object.type = 2
              AND index_object.is_disabled = 0
              AND indexed_column.name = @columnName
              AND index_column.is_descending_key = 0
              AND LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(ISNULL(index_object.filter_definition, N''), N'[', N''), N']', N''), N' ', N''), N'(', N''), N')', N'')) = @normalizedFilter
              AND NOT EXISTS
              (
                  SELECT 1 FROM sys.index_columns AS extra_column
                  WHERE extra_column.object_id = index_object.object_id
                    AND extra_column.index_id = index_object.index_id
                    AND (extra_column.key_ordinal > 1 OR extra_column.is_included_column = 1)
              )
        )
            SET @canonicalCorrect = 1;

        IF EXISTS
        (
            SELECT 1 FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'dbo.' + @tableName)
              AND name = @canonicalName
        )
        AND @canonicalCorrect = 0
        AND NOT EXISTS
        (
            SELECT 1
            FROM sys.indexes AS index_object
            INNER JOIN sys.index_columns AS index_column
                ON index_column.object_id = index_object.object_id
               AND index_column.index_id = index_object.index_id
               AND index_column.key_ordinal = 1
            INNER JOIN sys.columns AS indexed_column
                ON indexed_column.object_id = index_column.object_id
               AND indexed_column.column_id = index_column.column_id
            WHERE index_object.object_id = OBJECT_ID(N'dbo.' + @tableName)
              AND index_object.name = @canonicalName
              AND index_object.is_unique = 1
              AND index_object.is_primary_key = 0
              AND indexed_column.name = @columnName
              AND NOT EXISTS
              (
                  SELECT 1 FROM sys.index_columns AS extra_column
                  WHERE extra_column.object_id = index_object.object_id
                    AND extra_column.index_id = index_object.index_id
                    AND extra_column.key_ordinal > 1
              )
        )
        BEGIN
            SET @errorMessage = CONCAT(N'Index name collision: dbo.', @tableName, N'.', @canonicalName, N'.');
            THROW 50008, @errorMessage, 1;
        END;

        WHILE 1 = 1
        BEGIN
            SET @legacyName = NULL;
            SET @isUniqueConstraint = NULL;

            SELECT TOP (1)
                @legacyName = index_object.name,
                @isUniqueConstraint = index_object.is_unique_constraint
            FROM sys.indexes AS index_object
            INNER JOIN sys.index_columns AS index_column
                ON index_column.object_id = index_object.object_id
               AND index_column.index_id = index_object.index_id
               AND index_column.key_ordinal = 1
            INNER JOIN sys.columns AS indexed_column
                ON indexed_column.object_id = index_column.object_id
               AND indexed_column.column_id = index_column.column_id
            WHERE index_object.object_id = OBJECT_ID(N'dbo.' + @tableName)
              AND index_object.is_unique = 1
              AND index_object.is_primary_key = 0
              AND indexed_column.name = @columnName
              AND (@canonicalCorrect = 0 OR index_object.name <> @canonicalName)
              AND NOT EXISTS
              (
                  SELECT 1 FROM sys.index_columns AS extra_column
                  WHERE extra_column.object_id = index_object.object_id
                    AND extra_column.index_id = index_object.index_id
                    AND extra_column.key_ordinal > 1
              )
            ORDER BY index_object.is_unique_constraint DESC, index_object.name;

            IF @legacyName IS NULL BREAK;

            IF @isUniqueConstraint = 1
                SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@tableName) + N' DROP CONSTRAINT ' + QUOTENAME(@legacyName) + N';';
            ELSE
                SET @sql = N'DROP INDEX ' + QUOTENAME(@legacyName) + N' ON dbo.' + QUOTENAME(@tableName) + N';';
            EXEC sys.sp_executesql @sql;
        END;

        IF @canonicalCorrect = 0
            EXEC sys.sp_executesql @createSql;

        FETCH NEXT FROM ux_cursor
            INTO @tableName, @columnName, @canonicalName, @normalizedFilter, @createSql;
    END;
    CLOSE ux_cursor;
    DEALLOCATE ux_cursor;

    DECLARE @expectedChecks TABLE
    (
        table_name SYSNAME NOT NULL,
        constraint_name SYSNAME NOT NULL PRIMARY KEY,
        check_expression NVARCHAR(MAX) NOT NULL,
        normalized_definition NVARCHAR(MAX) NOT NULL
    );

    INSERT INTO @expectedChecks (table_name, constraint_name, check_expression, normalized_definition)
    VALUES
        (N'users', N'chk_user_role', N'[role] IN (''giam_doc'', ''admin_it'', ''ql_hcns'', ''nv_hcns'', ''ql_booking'', ''nv_booking'', ''ql_y_tuong'', ''nv_y_tuong'')', N'role=''nv_y_tuong''orrole=''ql_y_tuong''orrole=''nv_booking''orrole=''ql_booking''orrole=''nv_hcns''orrole=''ql_hcns''orrole=''admin_it''orrole=''giam_doc'''),
        (N'users', N'chk_user_status', N'[status] IN (''active'', ''locked'')', N'status=''locked''orstatus=''active'''),
        (N'employees', N'chk_emp_dept', N'[department] IN (''HCNS'', ''Booking'', ''Y_tuong'', ''IT'')', N'department=''it''ordepartment=''y_tuong''ordepartment=''booking''ordepartment=''hcns'''),
        (N'employees', N'chk_emp_contract', N'[contract_type] IN (''thu_viec'', ''chinh_thuc_1_nam'', ''vo_thoi_han'')', N'contract_type=''vo_thoi_han''orcontract_type=''chinh_thuc_1_nam''orcontract_type=''thu_viec'''),
        (N'employees', N'chk_emp_status', N'[status] IN (''dang_lam_viec'', ''thu_viec'', ''cho_duyet_nghi'', ''ngung_hoat_dong'')', N'status=''ngung_hoat_dong''orstatus=''cho_duyet_nghi''orstatus=''thu_viec''orstatus=''dang_lam_viec'''),
        (N'kols', N'chk_kol_platform', N'[platform] IN (''TikTok'', ''Instagram'', ''YouTube'', ''Facebook'')', N'platform=''facebook''orplatform=''youtube''orplatform=''instagram''orplatform=''tiktok'''),
        (N'kols', N'chk_kol_rating', N'[rating_score] BETWEEN 1 AND 5', N'rating_score>=1andrating_score<=5'),
        (N'kols', N'chk_kol_status', N'[status] IN (''tiem_nang'', ''da_lien_he'', ''dang_deal'', ''da_chot'', ''dang_chay'', ''hoan_thanh'')', N'status=''hoan_thanh''orstatus=''dang_chay''orstatus=''da_chot''orstatus=''dang_deal''orstatus=''da_lien_he''orstatus=''tiem_nang'''),
        (N'bookings', N'chk_booking_status', N'[status] IN (''dang_cho'', ''thuong_luong'', ''da_chot'', ''dang_trien_khai'', ''hoan_thanh'', ''huy'')', N'status=''huy''orstatus=''hoan_thanh''orstatus=''dang_trien_khai''orstatus=''da_chot''orstatus=''thuong_luong''orstatus=''dang_cho'''),
        (N'ideas', N'chk_idea_cat', N'[category] IN (''trend'', ''viral'', ''da_trien_khai'', ''chua_su_dung'')', N'category=''chua_su_dung''orcategory=''da_trien_khai''orcategory=''viral''orcategory=''trend'''),
        (N'ideas', N'chk_idea_status', N'[status] IN (''y_tuong'', ''review'', ''need_revision'', ''approved'', ''done'')', N'status=''done''orstatus=''approved''orstatus=''need_revision''orstatus=''review''orstatus=''y_tuong'''),
        (N'system_audit_logs', N'chk_log_module', N'[module] IN (''Nhan_Su'', ''Booking'', ''Y_Tuong'', ''Tai_Khoan'', ''Cau_Hinh'')', N'module=''cau_hinh''ormodule=''tai_khoan''ormodule=''y_tuong''ormodule=''booking''ormodule=''nhan_su''');

    DECLARE @constraintName SYSNAME;
    DECLARE @checkExpression NVARCHAR(MAX);
    DECLARE @expectedDefinition NVARCHAR(MAX);
    DECLARE @actualDefinition NVARCHAR(MAX);

    DECLARE check_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT table_name, constraint_name, check_expression, normalized_definition
        FROM @expectedChecks ORDER BY table_name, constraint_name;
    OPEN check_cursor;
    FETCH NEXT FROM check_cursor INTO @tableName, @constraintName, @checkExpression, @expectedDefinition;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @actualDefinition = NULL;
        SELECT @actualDefinition = LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(definition, N'[', N''), N']', N''), N' ', N''), N'(', N''), N')', N''))
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.' + @tableName) AND name = @constraintName;

        IF @actualDefinition IS NOT NULL AND @actualDefinition <> @expectedDefinition
        BEGIN
            SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@tableName) + N' DROP CONSTRAINT ' + QUOTENAME(@constraintName) + N';';
            EXEC sys.sp_executesql @sql;
            SET @actualDefinition = NULL;
        END;

        IF @actualDefinition IS NULL
        BEGIN
            SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@tableName)
                + N' WITH CHECK ADD CONSTRAINT ' + QUOTENAME(@constraintName)
                + N' CHECK (' + @checkExpression + N');';
            EXEC sys.sp_executesql @sql;
        END;

        SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@tableName)
            + N' WITH CHECK CHECK CONSTRAINT ' + QUOTENAME(@constraintName) + N';';
        EXEC sys.sp_executesql @sql;

        FETCH NEXT FROM check_cursor INTO @tableName, @constraintName, @checkExpression, @expectedDefinition;
    END;
    CLOSE check_cursor;
    DEALLOCATE check_cursor;

    DECLARE @expectedIndexes TABLE
    (
        table_name SYSNAME NOT NULL,
        index_name SYSNAME NOT NULL PRIMARY KEY,
        is_unique BIT NOT NULL,
        key_signature NVARCHAR(500) NOT NULL,
        normalized_filter NVARCHAR(500) NOT NULL,
        include_signature NVARCHAR(500) NOT NULL,
        create_sql NVARCHAR(MAX) NULL
    );

    INSERT INTO @expectedIndexes
        (table_name, index_name, is_unique, key_signature, normalized_filter, include_signature, create_sql)
    VALUES
        (N'booking_wage_audit_logs', N'IX_booking_wage_audit_logs_booking_id', 0, N'booking_id:A', N'', N'', N'CREATE INDEX IX_booking_wage_audit_logs_booking_id ON dbo.booking_wage_audit_logs(booking_id);'),
        (N'booking_wage_audit_logs', N'IX_booking_wage_audit_logs_performed_by_user_id', 0, N'performed_by_user_id:A', N'', N'', N'CREATE INDEX IX_booking_wage_audit_logs_performed_by_user_id ON dbo.booking_wage_audit_logs(performed_by_user_id);'),
        (N'booking_wages', N'IX_booking_wages_employee_id', 0, N'employee_id:A', N'', N'', N'CREATE INDEX IX_booking_wages_employee_id ON dbo.booking_wages(employee_id);'),
        (N'bookings', N'IX_bookings_kol_id', 0, N'kol_id:A', N'', N'', N'CREATE INDEX IX_bookings_kol_id ON dbo.bookings(kol_id);'),
        (N'bookings', N'IX_bookings_primary_manager_id', 0, N'primary_manager_id:A', N'', N'', N'CREATE INDEX IX_bookings_primary_manager_id ON dbo.bookings(primary_manager_id);'),
        (N'employees', N'IX_employees_manager_id', 0, N'manager_id:A', N'', N'', N'CREATE INDEX IX_employees_manager_id ON dbo.employees(manager_id);'),
        (N'ideas', N'IX_ideas_creator_employee_id', 0, N'creator_employee_id:A', N'', N'', N'CREATE INDEX IX_ideas_creator_employee_id ON dbo.ideas(creator_employee_id);'),
        (N'ideas', N'IX_ideas_primary_staff_id', 0, N'primary_staff_id:A', N'', N'', N'CREATE INDEX IX_ideas_primary_staff_id ON dbo.ideas(primary_staff_id);'),
        (N'ideas', N'IX_ideas_reviewer_employee_id', 0, N'reviewer_employee_id:A', N'', N'', N'CREATE INDEX IX_ideas_reviewer_employee_id ON dbo.ideas(reviewer_employee_id);'),
        (N'kols', N'IX_kols_responsible_staff_id', 0, N'responsible_staff_id:A', N'', N'', N'CREATE INDEX IX_kols_responsible_staff_id ON dbo.kols(responsible_staff_id);'),
        (N'system_audit_logs', N'idx_audit_logs_login_history', 0, N'user_id:A,created_at:D', N'module=''tai_khoan''anduser_idisnotnull', N'action_type,ip_address,device_info', N'CREATE INDEX idx_audit_logs_login_history ON dbo.system_audit_logs(user_id, created_at DESC) INCLUDE(action_type, ip_address, device_info) WHERE module = ''Tai_Khoan'' AND user_id IS NOT NULL;'),
        (N'users', N'idx_users_status_role', 0, N'status:A,role:A', N'', N'username,email', N'CREATE INDEX idx_users_status_role ON dbo.users(status, role) INCLUDE(username, email);'),
        (N'employees', N'UX_employees_email', 1, N'email:A', N'', N'', NULL),
        (N'employees', N'UX_employees_user_id', 1, N'user_id:A', N'user_idisnotnull', N'', NULL),
        (N'users', N'UX_users_email', 1, N'email:A', N'', N'', NULL),
        (N'users', N'UX_users_username', 1, N'username:A', N'', N'', NULL);

    DECLARE @indexName SYSNAME;
    DECLARE @isUnique BIT;
    DECLARE @expectedKey NVARCHAR(500);
    DECLARE @expectedInclude NVARCHAR(500);
    DECLARE @indexId INT;
    DECLARE @actualKey NVARCHAR(500);
    DECLARE @actualInclude NVARCHAR(500);
    DECLARE @actualFilter NVARCHAR(500);

    DECLARE index_create_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT table_name, index_name, create_sql FROM @expectedIndexes
        WHERE create_sql IS NOT NULL ORDER BY table_name, index_name;
    OPEN index_create_cursor;
    FETCH NEXT FROM index_create_cursor INTO @tableName, @indexName, @createSql;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.' + @tableName) AND name = @indexName)
            EXEC sys.sp_executesql @createSql;
        FETCH NEXT FROM index_create_cursor INTO @tableName, @indexName, @createSql;
    END;
    CLOSE index_create_cursor;
    DEALLOCATE index_create_cursor;

    DECLARE index_verify_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT table_name, index_name, is_unique, key_signature, normalized_filter, include_signature
        FROM @expectedIndexes ORDER BY table_name, index_name;
    OPEN index_verify_cursor;
    FETCH NEXT FROM index_verify_cursor
        INTO @tableName, @indexName, @isUnique, @expectedKey, @normalizedFilter, @expectedInclude;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @indexId = NULL;
        SET @actualKey = NULL;
        SET @actualInclude = N'';
        SET @actualFilter = N'';

        SELECT @indexId = index_id,
               @actualFilter = LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(ISNULL(filter_definition, N''), N'[', N''), N']', N''), N' ', N''), N'(', N''), N')', N''))
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.' + @tableName)
          AND name = @indexName
          AND type = 2
          AND is_unique = @isUnique
          AND is_primary_key = 0
          AND is_unique_constraint = 0
          AND is_disabled = 0;

        IF @indexId IS NOT NULL
        BEGIN
            SELECT @actualKey = STRING_AGG(
                    CONVERT(NVARCHAR(MAX), indexed_column.name + CASE WHEN index_column.is_descending_key = 1 THEN N':D' ELSE N':A' END), N',')
                    WITHIN GROUP (ORDER BY index_column.key_ordinal)
            FROM sys.index_columns AS index_column
            INNER JOIN sys.columns AS indexed_column
                ON indexed_column.object_id = index_column.object_id
               AND indexed_column.column_id = index_column.column_id
            WHERE index_column.object_id = OBJECT_ID(N'dbo.' + @tableName)
              AND index_column.index_id = @indexId AND index_column.key_ordinal > 0;

            SELECT @actualInclude = COALESCE(
                    STRING_AGG(CONVERT(NVARCHAR(MAX), indexed_column.name), N',')
                        WITHIN GROUP (ORDER BY index_column.index_column_id), N'')
            FROM sys.index_columns AS index_column
            INNER JOIN sys.columns AS indexed_column
                ON indexed_column.object_id = index_column.object_id
               AND indexed_column.column_id = index_column.column_id
            WHERE index_column.object_id = OBJECT_ID(N'dbo.' + @tableName)
              AND index_column.index_id = @indexId AND index_column.is_included_column = 1;
        END;

        IF @indexId IS NULL OR @actualKey <> @expectedKey OR @actualFilter <> @normalizedFilter OR @actualInclude <> @expectedInclude
        BEGIN
            SET @errorMessage = CONCAT(N'Index không khớp model: dbo.', @tableName, N'.', @indexName, N'.');
            THROW 50009, @errorMessage, 1;
        END;

        FETCH NEXT FROM index_verify_cursor
            INTO @tableName, @indexName, @isUnique, @expectedKey, @normalizedFilter, @expectedInclude;
    END;
    CLOSE index_verify_cursor;
    DEALLOCATE index_verify_cursor;

    IF EXISTS
    (
        SELECT 1 FROM @expectedPrimaryKeys AS expected
        LEFT JOIN sys.key_constraints AS actual
            ON actual.parent_object_id = OBJECT_ID(N'dbo.' + expected.table_name)
           AND actual.name = expected.canonical_name AND actual.type = N'PK'
        WHERE actual.object_id IS NULL
    )
        THROW 50006, 'Thiếu primary key canonical sau normalize.', 1;

    IF EXISTS
    (
        SELECT 1 FROM @expectedForeignKeys AS expected
        LEFT JOIN sys.foreign_keys AS actual
            ON actual.parent_object_id = OBJECT_ID(N'dbo.' + expected.parent_table)
           AND actual.name = expected.canonical_name
        WHERE actual.object_id IS NULL OR actual.is_disabled = 1 OR actual.is_not_trusted = 1
           OR actual.delete_referential_action <> expected.delete_action OR actual.update_referential_action <> 0
    )
        THROW 50007, 'Foreign key canonical thiếu, disabled, untrusted hoặc sai action.', 1;

    IF EXISTS
    (
        SELECT 1 FROM @expectedChecks AS expected
        LEFT JOIN sys.check_constraints AS actual
            ON actual.parent_object_id = OBJECT_ID(N'dbo.' + expected.table_name)
           AND actual.name = expected.constraint_name
        WHERE actual.object_id IS NULL OR actual.is_disabled = 1 OR actual.is_not_trusted = 1
           OR LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(actual.definition, N'[', N''), N']', N''), N' ', N''), N'(', N''), N')', N'')) <> expected.normalized_definition
    )
        THROW 50010, 'Check constraint thiếu, disabled, untrusted hoặc sai definition.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.users') AND name = N'status'
          AND TYPE_NAME(user_type_id) = N'varchar' AND max_length = 20 AND is_nullable = 0
    )
        THROW 50004, 'Final verification thất bại cho dbo.users.status.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.users') AND name = N'security_stamp'
          AND TYPE_NAME(user_type_id) = N'uniqueidentifier' AND max_length = 16 AND is_nullable = 0
    )
        THROW 50004, 'Final verification thất bại cho dbo.users.security_stamp.', 1;

    IF EXISTS
    (
        SELECT 1 FROM dbo.__EFMigrationsHistory
        WHERE MigrationId = @migrationId AND ProductVersion <> @productVersion
    )
        THROW 50011, 'Migration history có ProductVersion không tương thích.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = @migrationId)
        INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (@migrationId, @productVersion);

    IF @markerValue IS NULL
    BEGIN
        EXEC sys.sp_addextendedproperty @name = @markerName, @value = @migrationId;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
