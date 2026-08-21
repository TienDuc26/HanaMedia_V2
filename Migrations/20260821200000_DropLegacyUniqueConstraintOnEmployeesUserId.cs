using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HanaMedia.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyUniqueConstraintOnEmployeesUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop các unique constraint / index thừa từ schema cũ (đặt tên theo bảng cũ "employee" số ít).
            // Bảng employees hiện tại đã có UX_employees_user_id với filter [user_id] IS NOT NULL,
            // nên các ràng buộc không filter này (nếu tồn tại) sẽ ngăn nhiều nhân viên có user_id = NULL.
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ__employee__6B19F4B6BF4CC83C' AND object_id = OBJECT_ID('dbo.employees'))
                    DROP INDEX [UQ__employee__6B19F4B6BF4CC83C] ON [dbo].[employees];
                """);

            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ__employees__user_id' AND object_id = OBJECT_ID('dbo.employees'))
                    DROP INDEX [UQ__employees__user_id] ON [dbo].[employees];
                """);

            // Phòng trường hợp db được tạo từ script cũ mà thiếu filter trên UX_employees_user_id:
            // tái tạo index có filter để chỉ unique khi user_id IS NOT NULL (cho phép nhiều nhân viên không gắn user).
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
            // Không khôi phục các constraint cũ; chỉ đảm bảo UX_employees_user_id còn lại đúng filter.
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_employees_user_id' AND object_id = OBJECT_ID('dbo.employees'))
                BEGIN
                    DROP INDEX [UX_employees_user_id] ON [dbo].[employees];
                    CREATE UNIQUE INDEX [UX_employees_user_id]
                        ON [dbo].[employees] ([user_id])
                        WHERE [user_id] IS NOT NULL;
                END
                """);
        }
    }
}
