using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HanaMedia.Migrations
{
    /// <inheritdoc />
    public partial class AddIdeaManagementModule13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'chk_idea_status' AND parent_object_id = OBJECT_ID('ideas'))
                    ALTER TABLE [ideas] DROP CONSTRAINT [chk_idea_status];
            ");

            migrationBuilder.AlterColumn<string>(
                name: "platform",
                table: "kols",
                type: "varchar(200)",
                unicode: false,
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldUnicode: false,
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "niche",
                table: "kols",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "campaign_name",
                table: "ideas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            // Một số DB local đã từng chạy migration Module 13 ở nhánh cũ. Các lệnh
            // dưới đây có điều kiện để pull nhánh mới không bị lỗi "already exists".
            migrationBuilder.Sql(@"
                IF COL_LENGTH('ideas', 'campaign_id') IS NULL
                    ALTER TABLE [ideas] ADD [campaign_id] int NULL;
                IF COL_LENGTH('ideas', 'moodboard_file_url') IS NULL
                    ALTER TABLE [ideas] ADD [moodboard_file_url] varchar(255) NULL;
                IF COL_LENGTH('ideas', 'reference_file_url') IS NULL
                    ALTER TABLE [ideas] ADD [reference_file_url] varchar(255) NULL;
                IF COL_LENGTH('bookings', 'campaign_id') IS NULL
                    ALTER TABLE [bookings] ADD [campaign_id] int NULL;

                IF OBJECT_ID(N'[idea_comments]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [idea_comments] (
                        [id] bigint IDENTITY(1,1) NOT NULL,
                        [idea_id] int NOT NULL,
                        [author_user_id] int NULL,
                        [comment_type] varchar(30) NOT NULL CONSTRAINT [DF_idea_comments_comment_type] DEFAULT 'general',
                        [content] nvarchar(max) NOT NULL,
                        [created_at] datetime2 NOT NULL CONSTRAINT [DF_idea_comments_created_at] DEFAULT (sysutcdatetime()),
                        CONSTRAINT [PK_idea_comments] PRIMARY KEY ([id])
                    );
                END;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE [ideas] ADD CONSTRAINT [chk_idea_status]
                    CHECK ([status] IN ('y_tuong','review','need_revision','approved','in_production','done'));

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ideas_campaign_id' AND object_id=OBJECT_ID('ideas'))
                    CREATE INDEX [IX_ideas_campaign_id] ON [ideas] ([campaign_id]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_bookings_campaign_id' AND object_id=OBJECT_ID('bookings'))
                    CREATE INDEX [IX_bookings_campaign_id] ON [bookings] ([campaign_id]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='idx_idea_comments_idea_created' AND object_id=OBJECT_ID('idea_comments'))
                    CREATE INDEX [idx_idea_comments_idea_created] ON [idea_comments] ([idea_id], [created_at]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_idea_comments_author_user_id' AND object_id=OBJECT_ID('idea_comments'))
                    CREATE INDEX [IX_idea_comments_author_user_id] ON [idea_comments] ([author_user_id]);

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_bookings_campaigns_campaign_id')
                    ALTER TABLE [bookings] ADD CONSTRAINT [FK_bookings_campaigns_campaign_id]
                        FOREIGN KEY ([campaign_id]) REFERENCES [campaigns]([id]) ON DELETE SET NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_ideas_campaigns_campaign_id')
                    ALTER TABLE [ideas] ADD CONSTRAINT [FK_ideas_campaigns_campaign_id]
                        FOREIGN KEY ([campaign_id]) REFERENCES [campaigns]([id]) ON DELETE SET NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_idea_comments_ideas_idea_id')
                    ALTER TABLE [idea_comments] ADD CONSTRAINT [FK_idea_comments_ideas_idea_id]
                        FOREIGN KEY ([idea_id]) REFERENCES [ideas]([id]) ON DELETE CASCADE;
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_idea_comments_users_author_user_id')
                    ALTER TABLE [idea_comments] ADD CONSTRAINT [FK_idea_comments_users_author_user_id]
                        FOREIGN KEY ([author_user_id]) REFERENCES [users]([id]) ON DELETE SET NULL;

                UPDATE i SET [campaign_id] = c.[id]
                FROM [ideas] i INNER JOIN [campaigns] c
                    ON c.[name] = i.[campaign_name] AND c.[client] = i.[client_name]
                WHERE i.[campaign_id] IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bookings_campaigns_campaign_id",
                table: "bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_ideas_campaigns_campaign_id",
                table: "ideas");

            migrationBuilder.DropTable(
                name: "idea_comments");

            migrationBuilder.DropIndex(
                name: "IX_ideas_campaign_id",
                table: "ideas");

            migrationBuilder.DropCheckConstraint(
                name: "chk_idea_status",
                table: "ideas");

            migrationBuilder.DropIndex(
                name: "IX_bookings_campaign_id",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "campaign_id",
                table: "ideas");

            migrationBuilder.DropColumn(
                name: "moodboard_file_url",
                table: "ideas");

            migrationBuilder.DropColumn(
                name: "reference_file_url",
                table: "ideas");

            migrationBuilder.DropColumn(
                name: "campaign_id",
                table: "bookings");

            migrationBuilder.AlterColumn<string>(
                name: "platform",
                table: "kols",
                type: "varchar(200)",
                unicode: false,
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldUnicode: false,
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "niche",
                table: "kols",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "campaign_name",
                table: "ideas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "chk_idea_status",
                table: "ideas",
                sql: "[status] IN ('y_tuong', 'review', 'need_revision', 'approved', 'done')");
        }
    }
}
