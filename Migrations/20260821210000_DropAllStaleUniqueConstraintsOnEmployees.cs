using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HanaMedia.Migrations
{
    /// <inheritdoc />
    public partial class DropAllStaleUniqueConstraintsOnEmployees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop TOÀN BỘ unique constraint / index trên bảng employees
            // mà không nằm trong whitelist (UX_employees_email, UX_employees_user_id).
            // Đây là cách an toàn để dọn dẹp các constraint rác từ schema cũ
            // (đặt tên theo bảng "employee" số ít hoặc tự sinh hash) đang ngăn
            // người dùng thêm nhân viên hợp lệ.
            migrationBuilder.Sql(
                """
                DECLARE @sql NVARCHAR(MAX) = N'';

                SELECT @sql = @sql + N'ALTER TABLE [dbo].[employees] DROP CONSTRAINT [' + name + N'];' + CHAR(10)
                FROM sys.key_constraints
                WHERE [type] = 'UQ'
                  AND [object_id] = OBJECT_ID('dbo.employees');

                SELECT @sql = @sql + N'DROP INDEX [' + name + N'] ON [dbo].[employees];' + CHAR(10)
                FROM sys.indexes
                WHERE object_id = OBJECT_ID('dbo.employees')
                  AND is_unique = 1
                  AND is_primary_key = 0
                  AND is_hypothetical = 0
                  AND name NOT IN (N'UX_employees_email', N'UX_employees_user_id');

                IF LEN(@sql) > 0 EXEC sp_executesql @sql;
                """);

            // Phòng trường hợp bảng employees được tạo từ script cũ mà thiếu
            // 2 unique index chuẩn, ép tạo lại để chắc chắn:
            //   - email phải unique
            //   - user_id chỉ unique khi không NULL
            migrationBuilder.Sql(
                """
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_employees_email' AND object_id = OBJECT_ID('dbo.employees'))
                    CREATE UNIQUE INDEX [UX_employees_email] ON [dbo].[employees] ([email]);
                """);

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'UX_employees_user_id'
                      AND object_id = OBJECT_ID('dbo.employees')
                      AND filter_definition IS NOT NULL
                )
                BEGIN
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_employees_user_id' AND object_id = OBJECT_ID('dbo.employees'))
                        DROP INDEX [UX_employees_user_id] ON [dbo].[employees];

                    CREATE UNIQUE INDEX [UX_employees_user_id]
                        ON [dbo].[employees] ([user_id])
                        WHERE [user_id] IS NOT NULL;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Khôi phục các index chuẩn nếu chẳng may bị mất
            migrationBuilder.Sql(
                """
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_employees_email' AND object_id = OBJECT_ID('dbo.employees'))
                    CREATE UNIQUE INDEX [UX_employees_email] ON [dbo].[employees] ([email]);
                """);

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'UX_employees_user_id'
                      AND object_id = OBJECT_ID('dbo.employees')
                )
                    CREATE UNIQUE INDEX [UX_employees_user_id]
                        ON [dbo].[employees] ([user_id])
                        WHERE [user_id] IS NOT NULL;
                """);
        }
    }
}