# Thiết lập database HanaMedia V2

Migrations trong Git là nguồn chuẩn duy nhất cho cấu trúc database. Mỗi lập trình
viên dùng database local riêng; không commit tài khoản, mật khẩu hoặc bản sao dữ
liệu thật lên Git. `database_schema.sql` chỉ là tài liệu legacy, không dùng để dựng
database mới hoặc đồng bộ schema giữa các thành viên.

## Database Development mới

Môi trường Development mặc định dùng Windows Authentication với
`.\\SQLEXPRESS/HanaMediaV2Dev`. Có thể ghi đè connection string cho máy riêng bằng
User Secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\\SQLEXPRESS;Database=HanaMediaV2Dev;Trusted_Connection=True;TrustServerCertificate=True"
dotnet tool restore
dotnet restore
dotnet tool run dotnet-ef database update --context ApplicationDbContext
```

Hoặc chỉ ghi đè cho phiên PowerShell hiện tại:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=.\\SQLEXPRESS;Database=HanaMediaV2Dev;Trusted_Connection=True;TrustServerCertificate=True'
```

### Tạo AdminIT đầu tiên

Lệnh bootstrap chỉ hoạt động trong `Development`, chỉ tạo tài khoản khi database
chưa có AdminIT và dùng cùng cơ chế hash hiện tại của ứng dụng. Đặt giá trị thật
trong User Secrets, không ghi chúng vào tài liệu hoặc source code:

```powershell
dotnet user-secrets set "BootstrapAdmin:Username" "<username>"
dotnet user-secrets set "BootstrapAdmin:Email" "<email>"
dotnet user-secrets set "BootstrapAdmin:Password" "<strong-temporary-password>"
dotnet run -- --bootstrap-admin
```

Sau khi bootstrap thành công, xóa ngay ba secret tạm:

```powershell
dotnet user-secrets remove "BootstrapAdmin:Username"
dotnet user-secrets remove "BootstrapAdmin:Email"
dotnet user-secrets remove "BootstrapAdmin:Password"
```

## Adopt database HanaMediaGithub đã có dữ liệu

Không chạy migration khởi tạo trực tiếp lên database đã có bảng. Trước khi adopt:

1. Tạo full backup và kiểm tra chắc chắn backup có thể restore.
2. Dừng ứng dụng và mọi tiến trình đang ghi vào database.
3. Kiểm tra đúng server/database đích bằng `SELECT @@SERVERNAME, DB_NAME();`.
4. Chạy script bằng tài khoản có quyền đổi schema.

Ví dụ dùng Windows Authentication từ thư mục repository:

```powershell
sqlcmd -S ".\\SQLEXPRESS" -E -C -b -d "HanaMediaGithub" -i ".\\database_patches\\20260820_adopt_v2_module2_migrations.sql"
```

Script chạy trong transaction, kiểm tra schema Module 2, chuẩn hóa tên PK/FK/index,
ghi migration history và tạo database-level marker `HanaMediaAdoptedBaseline`.
Không xóa hoặc sửa marker này. Sau đó kiểm tra:

```sql
SELECT MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory;
SELECT name, value
FROM sys.extended_properties
WHERE class = 0 AND name = N'HanaMediaAdoptedBaseline';
```

Migration chỉ đồng bộ cấu trúc. Dữ liệu thật phải chuyển bằng backup/BACPAC phù hợp.

> **Không chạy `dotnet ef database update 0` trên database đã adopt.** Initial
> migration có `Down()` xóa các bảng baseline. Guard marker sẽ chặn thao tác này,
> nhưng phương án rollback an toàn vẫn là restore bản backup đã kiểm tra.

## Quy trình migration khi làm việc nhóm

- Toàn dự án chỉ có một migration chain cho `ApplicationDbContext`.
- Trước khi sửa model hoặc tạo migration, pull/rebase nhánh tích hợp và chạy toàn bộ
  migration mới trên database local.
- Nếu hai nhánh cùng sửa model, thống nhất một người tạo migration cuối cùng. Nhánh
  còn lại rebase rồi regenerate migration của mình; không merge thủ công snapshot.
- Không sửa, đổi tên hoặc xóa migration đã được áp dụng trên database dùng chung.
- Commit đồng thời file migration, file Designer, model snapshot và tool manifest.
- Sau mỗi lần pull có migration mới, chạy:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --context ApplicationDbContext
```
