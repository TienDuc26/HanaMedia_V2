using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HanaMedia.Migrations
{
    /// <inheritdoc />
    public partial class AddIdeaMoodboardGalleryModule13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tương thích với DB đã từng chạy migration Module 13 trên nhánh cũ.
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[idea_moodboard_images]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [idea_moodboard_images] (
                        [id] bigint IDENTITY(1,1) NOT NULL,
                        [idea_id] int NOT NULL,
                        [file_url] varchar(255) NOT NULL,
                        [sort_order] int NOT NULL,
                        [created_at] datetime2 NOT NULL CONSTRAINT [DF_idea_moodboard_images_created_at] DEFAULT (sysutcdatetime()),
                        CONSTRAINT [PK_idea_moodboard_images] PRIMARY KEY ([id])
                    );
                END;
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='idx_idea_moodboard_images_idea_sort' AND object_id=OBJECT_ID('idea_moodboard_images'))
                    CREATE INDEX [idx_idea_moodboard_images_idea_sort] ON [idea_moodboard_images] ([idea_id], [sort_order]);
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_idea_moodboard_images_ideas_idea_id')
                    ALTER TABLE [idea_moodboard_images] ADD CONSTRAINT [FK_idea_moodboard_images_ideas_idea_id]
                        FOREIGN KEY ([idea_id]) REFERENCES [ideas]([id]) ON DELETE CASCADE;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idea_moodboard_images");
        }
    }
}
