using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HanaMedia.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalAndAccountantRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'chk_user_role' AND parent_object_id = OBJECT_ID('users'))
                BEGIN
                    ALTER TABLE [users] DROP CONSTRAINT [chk_user_role];
                END;
                ALTER TABLE [users] ADD CONSTRAINT [chk_user_role] CHECK ([role] IN ('giam_doc', 'admin_it', 'ql_hcns', 'nv_hcns', 'ql_booking', 'nv_booking', 'ql_y_tuong', 'nv_y_tuong', 'nv_phap_ly', 'nv_ke_toan'));
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'chk_user_role' AND parent_object_id = OBJECT_ID('users'))
                BEGIN
                    ALTER TABLE [users] DROP CONSTRAINT [chk_user_role];
                END;
                ALTER TABLE [users] ADD CONSTRAINT [chk_user_role] CHECK ([role] IN ('giam_doc', 'admin_it', 'ql_hcns', 'nv_hcns', 'ql_booking', 'nv_booking', 'ql_y_tuong', 'nv_y_tuong'));
            ");
        }
    }
}
