using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HanaMedia.Migrations
{
    /// <inheritdoc />
    public partial class SeedDirectorUserAndEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tạo sẵn 1 tài khoản Giám đốc — không cần gán Employee,
            // Director mặc định đã là 1 nhân viên có role Giám đốc.
            // Username: giamdoc  |  Password: GiamDoc@2026
            migrationBuilder.Sql(@"
                DECLARE @exists INT;
                SELECT @exists = COUNT(1) FROM [users] WHERE [username] = 'giamdoc';
                IF @exists = 0
                BEGIN
                    INSERT INTO [users] ([username], [email], [password_hash], [role], [status], [security_stamp], [created_at], [updated_at])
                    VALUES (
                        'giamdoc',
                        'director@hanamedia.com',
                        '343abcc1046b548ba7f98f89fd71ce36a82a773b6a1f4ca0f260e5803c9f452c',
                        'giam_doc',
                        'active',
                        NEWID(),
                        GETUTCDATE(),
                        GETUTCDATE()
                    );
                END
            ");

            // Tạo Employee record cho Giám đốc, link với user vừa tạo
            migrationBuilder.Sql(@"
                DECLARE @directorUserId INT;
                SELECT @directorUserId = [id] FROM [users] WHERE [username] = 'giamdoc';

                IF @directorUserId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [employees] WHERE [user_id] = @directorUserId)
                BEGIN
                    INSERT INTO [employees] (
                        [user_id], [full_name], [avatar_url], [dob], [phone], [email],
                        [address], [joined_date], [department], [position],
                        [is_manager], [manager_id], [contract_type],
                        [basic_salary], [status], [created_at], [updated_at]
                    )
                    VALUES (
                        @directorUserId,
                        N'Giám đốc',
                        NULL,
                        '1980-01-01',
                        '0900000000',
                        'director@hanamedia.com',
                        N'Hồ Chí Minh',
                        '2020-01-01',
                        'HCNS',
                        N'Giám đốc',
                        1,
                        NULL,
                        'vo_thoi_han',
                        50000000,
                        'dang_lam_viec',
                        GETUTCDATE(),
                        GETUTCDATE()
                    );
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DECLARE @directorUserId INT;
                SELECT @directorUserId = [id] FROM [users] WHERE [username] = 'giamdoc';
                IF @directorUserId IS NOT NULL
                BEGIN
                    DELETE FROM [employees] WHERE [user_id] = @directorUserId;
                    DELETE FROM [users] WHERE [id] = @directorUserId;
                END
            ");
        }
    }
}
