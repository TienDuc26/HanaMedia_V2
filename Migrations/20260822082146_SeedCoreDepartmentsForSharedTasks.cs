using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HanaMedia.Migrations
{
    /// <inheritdoc />
    public partial class SeedCoreDepartmentsForSharedTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF NOT EXISTS (SELECT 1 FROM [departments] WHERE [code] = N'HCNS')
                    INSERT INTO [departments] ([code], [name], [description], [status])
                    VALUES (N'HCNS', N'Hành chính nhân sự', N'Phòng ban nền của HanaMedia', N'active');
                IF NOT EXISTS (SELECT 1 FROM [departments] WHERE [code] = N'Booking')
                    INSERT INTO [departments] ([code], [name], [description], [status])
                    VALUES (N'Booking', N'Booking', N'Phòng ban nền của HanaMedia', N'active');
                IF NOT EXISTS (SELECT 1 FROM [departments] WHERE [code] = N'Y_tuong')
                    INSERT INTO [departments] ([code], [name], [description], [status])
                    VALUES (N'Y_tuong', N'Ý tưởng', N'Phòng ban nền của HanaMedia', N'active');
                IF NOT EXISTS (SELECT 1 FROM [departments] WHERE [code] = N'IT')
                    INSERT INTO [departments] ([code], [name], [description], [status])
                    VALUES (N'IT', N'Công nghệ thông tin', N'Phòng ban nền của HanaMedia', N'active');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM [departments]
                WHERE [description] = N'Phòng ban nền của HanaMedia'
                  AND [code] IN (N'HCNS', N'Booking', N'Y_tuong', N'IT')
                  AND NOT EXISTS (SELECT 1 FROM [employees] WHERE [employees].[department] = [departments].[code]);
                """);
        }
    }
}
