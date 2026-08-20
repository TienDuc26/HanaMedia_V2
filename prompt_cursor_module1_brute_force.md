# Prompt gửi Cursor — Bổ sung Module 1: Chống dò mật khẩu (Auto-lock)

Tôi đính kèm `requirement_HanaMedia_v2.md` bản mới nhất, đặc biệt xem **mục 10.1 – Chống dò mật khẩu (Brute-force protection)**. Đây là bổ sung cho Module 1 (Đăng nhập & Bảo mật mạng) **đã code trước đó** — nhiệm vụ lần này là **gắn thêm** cơ chế khóa tạm vào luồng đăng nhập hiện có, không viết lại từ đầu.

## Yêu cầu chi tiết (lấy đúng theo mục 10.1, không thêm bớt)
1. Đếm số lần đăng nhập sai liên tiếp **theo từng tài khoản (username)**, **không đếm theo IP** (nhiều máy trong công ty dùng chung 1 dải IP, đếm theo IP dễ khóa nhầm người khác)
2. Ngưỡng: **5 lần sai liên tiếp trong vòng 15 phút** → tài khoản bị **khóa tạm thời 15 phút**
3. Trong thời gian khóa tạm: từ chối đăng nhập **dù nhập đúng mật khẩu**, hiển thị popup thông báo **riêng biệt**, khác với:
   - Popup sai tài khoản/mật khẩu (đã có)
   - Popup sai mạng nội bộ (đã có)
   → Nội dung gợi ý: "Tài khoản tạm thời bị khóa do đăng nhập sai quá nhiều lần. Vui lòng thử lại sau ít phút hoặc liên hệ quản trị viên."
4. Hết 15 phút → tự động mở khóa, đếm lại từ đầu (không cần thao tác gì thêm)
5. Đăng nhập **thành công** trước khi đạt ngưỡng → reset bộ đếm về 0 ngay lập tức
6. AdminIT vẫn phải mở khóa sớm được bất cứ lúc nào bằng chức năng **Khóa/mở tài khoản đã có sẵn ở Module 2** — không tạo thêm 1 cơ chế mở khóa riêng, dùng chung đúng chức năng đó
7. Mỗi lần hệ thống **tự động khóa tạm** (không phải AdminIT khóa tay) phải ghi 1 dòng vào Audit Log như cảnh báo bất thường — **nếu Module 3 (Audit Log) đã có sẵn service log dùng chung, gọi qua service đó**; nếu Module 3 **chưa được code**, chỉ cần ghi log tạm ra console/log file kèm ghi chú rõ ràng bằng comment code rằng "cần nối vào Audit Log service khi Module 3 hoàn thành" — không tự ý tạo bảng log riêng cho việc này

## Yêu cầu bắt buộc về Database
- Trước khi sửa DB, kiểm tra bảng User/Account hiện có (từ Module 2) đã có field nào phục vụ được việc này chưa (VD: `failed_login_attempts`, `locked_until`...)
- Nếu chưa có, **thêm field vào đúng bảng User/Account hiện tại** (không tạo bảng phụ riêng cho việc đếm số lần sai) — tối thiểu cần: số lần đăng nhập sai liên tiếp hiện tại, thời điểm khóa hết hạn (nếu đang bị khóa)
- Đề xuất tên field + kiểu dữ liệu cụ thể, báo cáo lại cho tôi **duyệt trước khi viết migration**
- **Bắt buộc tạo migration** (không sửa tay DB), migration phải commit được vào Git để nhóm `pull` về chạy migrate là có ngay, đặt tên migration rõ nghĩa (VD: `add_login_lockout_fields_to_users_table`)

## Quy tắc giữ nguyên như các lần trước
- Không tạo file/folder mới nếu không cần; nếu bắt buộc phải tạo mới → dừng lại, xin xác nhận trước
- Không tạo file rác, không để lại code debug thừa
- Bám đúng cấu trúc MVC hiện có, logic đếm/khóa nên đặt ở đúng layer (service/middleware xử lý login), không nhét thẳng vào Controller
- Toàn bộ logic phải bọc `try/catch`, không được để lỗi ở phần này làm sập luồng đăng nhập bình thường hay làm crash server

## Việc cần làm theo thứ tự
1. Khảo sát bảng User/Account hiện tại, logic xử lý login hiện tại ở Module 1 đang nằm ở file nào
2. Đề xuất field cần thêm vào bảng User/Account → báo cáo, chờ tôi xác nhận
3. Viết migration sau khi được duyệt
4. Cập nhật logic login: tăng bộ đếm khi sai, reset khi đúng, kiểm tra khóa tạm trước khi cho thử mật khẩu, set thời điểm hết khóa khi đạt ngưỡng
5. Thêm popup thông báo riêng cho trường hợp bị khóa tạm
6. Gắn log cảnh báo (qua Module 3 nếu đã có, tạm thời console log nếu chưa)
7. Báo cáo cuối: field đã thêm, migration đã tạo, file đã sửa, kết quả test (đăng nhập sai 5 lần liên tiếp → xác nhận bị khóa đúng 15 phút, đăng nhập đúng giữa chừng → xác nhận bộ đếm reset, AdminIT mở khóa sớm → xác nhận vào lại được ngay)
