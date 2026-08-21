# Module 3 - Audit Log

`ISystemAuditService` là cổng ghi nhật ký dùng chung. Các module nghiệp vụ không tự ghi trực tiếp vào `system_audit_logs`.

## Ghi log cùng giao dịch nghiệp vụ

Khi controller/service đang thay đổi dữ liệu bằng `ApplicationDbContext`, gọi `AddEvent` trước `SaveChangesAsync`. Dữ liệu nghiệp vụ và nhật ký sẽ được lưu trong cùng lần lưu:

```csharp
auditService.AddEvent(new AuditEvent(
    AuditModules.HumanResources,
    AuditActions.Updated,
    "Cập nhật trạng thái nhân viên.",
    TargetType: "employee",
    TargetId: employee.Id.ToString()));

await context.SaveChangesAsync(cancellationToken);
```

Actor được lấy từ claim `NameIdentifier`; IP, thiết bị và thời gian được lấy từ request hiện tại. Không truyền tên người dùng từ form vì dữ liệu đó có thể bị giả mạo.

## Ghi log cho action đơn giản

Với action POST/PUT/DELETE thành công và không cần cùng transaction, có thể gắn attribute:

```csharp
[AuditMutation(
    AuditModules.Booking,
    AuditActions.Approved,
    "Duyệt booking.",
    TargetType = "booking",
    TargetIdArgument = "id")]
```

Không dùng attribute cho GET. Action trả HTTP 4xx/5xx hoặc ném exception sẽ không được ghi là thành công.

## Quy ước

- Module dùng hằng số trong `AuditModules`.
- Action dùng hằng số trong `AuditActions`; mã chỉ gồm chữ thường, số và dấu gạch dưới.
- Nội dung không chứa mật khẩu, hash, cookie, token hoặc dữ liệu nhạy cảm.
- Xóa nhiều bản ghi trong một thao tác dùng `AuditActions.BulkDelete` để kích hoạt cảnh báo.
- AdminIT xem chi tiết, lọc, phân trang và xuất CSV tại `/AdminIT/AuditLog`.
- Giám đốc chỉ xem số lượng tổng hợp tại `/Director/MonitoringSystem`, không nhận actor, IP hoặc nội dung log.
