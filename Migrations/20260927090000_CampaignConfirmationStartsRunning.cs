using HanaMedia.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HanaMedia.Migrations;

// Data-only migration: existing status codes and schema remain compatible.
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260927090000_CampaignConfirmationStartsRunning")]
public sealed class CampaignConfirmationStartsRunning : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE @changed TABLE (id int, old_status nvarchar(50), new_status nvarchar(50));
            UPDATE campaigns
            SET status = CASE WHEN ConfirmedAt IS NULL THEN 'planning' ELSE 'running' END
            OUTPUT inserted.id, deleted.status, inserted.status INTO @changed
            WHERE status IN ('planning','running','paused')
              AND status <> CASE WHEN ConfirmedAt IS NULL THEN 'planning' ELSE 'running' END;

            INSERT INTO system_audit_logs (user_id, action_type, module, log_detail, ip_address, created_at)
            SELECT NULL, 'campaign_status_aligned', 'Booking',
                CONCAT(N'[Campaign#', id, N'] Đồng bộ luồng Giám đốc chốt: ', old_status, N' -> ', new_status,
                       N'. Không thay đổi bằng chứng chốt, Booking hoặc ý tưởng liên quan.'),
                'migration', GETDATE()
            FROM @changed;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally retain normalized business data on code rollback.
        // Old states are recorded in audit; use the pre-migration backup for exact recovery.
    }
}
