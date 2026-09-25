# Lộ trình triển khai Module — Web App Quản lý Nhân sự HanaMedia

Nguyên tắc sắp xếp: module đứng trước là **nền tảng dữ liệu/quyền** mà module đứng sau bắt buộc phải tham chiếu tới (VD: Booking cần có sẵn danh sách nhân viên và campaign; Chia thù lao cần có sẵn Booking). Làm đúng thứ tự để tránh phải sửa lại module trước khi module sau đã cắm vào.

Nội dung từng module lấy **đúng 100% theo file requirement** (`requirement_HanaMedia_v2.md`) — không thêm, không bớt.

---

## Giai đoạn 0 — Nền tảng bắt buộc (làm trước tiên, mọi module sau đều phụ thuộc)

### Module 1: Đăng nhập & Bảo mật truy cập mạng
- Đăng nhập/đăng xuất
- Kiểm soát whitelist IP nội bộ (chỉ cho đăng nhập khi trong mạng LAN/Wifi công ty; chặn 4G/5G, wifi ngoài)
- Thông báo rõ ràng khi bị chặn do sai mạng (không lẫn với lỗi sai tài khoản/mật khẩu)
- **Phụ thuộc:** không phụ thuộc module nào — đây là lớp chặn ở tầng server/firewall trước khi request chạm ứng dụng
- **Vì sao làm đầu tiên:** mọi module khác đều cần người dùng đăng nhập được mới thao tác

### Module 2: Quản lý tài khoản & Phân quyền (AdminIT)
- Tạo tài khoản, khóa/mở tài khoản, phân quyền, thay đổi role, reset mật khẩu
- Xem lịch sử đăng nhập (thời gian đăng nhập/đăng xuất, thiết bị/IP)
- Cơ chế RBAC (8 role: Giám đốc, AdminIT, QL/NV HCNS, QL/NV Booking, QL/NV Ý tưởng) áp theo đúng ma trận phân quyền mục 1
- **Phụ thuộc:** Module 1
- **Vì sao làm sớm:** tất cả module nghiệp vụ phía sau đều cần biết role của người dùng để ẩn/hiện đúng chức năng; không có module này thì không test được phân quyền của các module sau

### Module 3: Nhật ký hệ thống (Audit Log) — phần lõi ghi log
- Cơ chế ghi log dùng chung: ai thao tác gì, khi nào, tác động vào dữ liệu nào (tạo/sửa/xóa nhân viên, booking, ý tưởng, phân bổ thù lao, duyệt/ký hợp đồng, đổi quyền tài khoản, truy cập mạng ngoài bị chặn...)
- Màn hình xem log cho AdminIT: lọc theo người dùng/loại hành động/module/khoảng thời gian, xuất báo cáo, cảnh báo bất thường (đăng nhập thất bại nhiều lần, xóa hàng loạt)
- Giám đốc: xem báo cáo tổng quan hoạt động hệ thống (không xem chi tiết)
- **Phụ thuộc:** Module 2 (cần biết ai đang thao tác)
- **Vì sao làm sớm:** cơ chế ghi log phải có sẵn *trước khi* các module nghiệp vụ (nhân sự, booking, ý tưởng...) được viết, để mỗi hành động tạo/sửa/xóa/duyệt ở các module sau tự động ghi log ngay từ đầu, tránh phải quay lại gắn log cho từng module sau này

---

## Giai đoạn 1 — Dữ liệu nền dùng chung

### Module 4: Quản lý phòng ban
- CRUD phòng ban (Giám đốc)
- **Phụ thuộc:** Module 2 (phân quyền)
- **Vì sao trước Module 5:** hồ sơ nhân viên cần gán vào phòng ban đã tồn tại

### Module 5: Quản lý nhân sự (Hồ sơ nhân viên)
- CRUD nhân viên (QL HCNS): thêm/sửa/xóa-ngừng hoạt động/chuyển phòng ban/đổi chức vụ/đổi trạng thái
- Thông tin nhân viên đầy đủ: họ tên, avatar, ngày sinh, SĐT, email, địa chỉ, ngày vào công ty, phòng ban, chức vụ, người quản lý, loại hợp đồng, trạng thái
- NV HCNS: xem danh sách, tạo/sửa hồ sơ theo quyền, cập nhật giấy tờ, theo dõi hợp đồng, quản lý hồ sơ
- Giám đốc: xem toàn bộ hồ sơ, lịch sử làm việc, chức vụ, phòng ban, mức lương/phụ cấp (nếu có), đánh giá hiệu suất, lịch sử khen thưởng/kỷ luật
- **Phụ thuộc:** Module 4
- **Vì sao trước Giai đoạn 2:** Booking và Ý tưởng đều cần chọn "người phụ trách"/"nhân viên tham gia" từ danh sách nhân viên đã tồn tại; nếu làm Booking/Ý tưởng trước sẽ phải mock dữ liệu nhân viên rồi sửa lại

### Module 6: Phân công công việc & Duyệt công việc (Task engine dùng chung)
- Tạo task, giao task, giao deadline, theo dõi tiến độ (dùng chung cho QL HCNS, QL Booking, QL Ý tưởng, Giám đốc)
- Duyệt công việc (Giám đốc, QL Booking, QL Ý tưởng)
- Trạng thái task: To do → In Progress → Review → Need Revision → Approved → Done (nhân viên không được tự chuyển sang Approved)
- **Phụ thuộc:** Module 5 (task phải gán được cho nhân viên có thật)
- **Vì sao trước Giai đoạn 2 & 3:** cả module Booking (mục "Quản lý nhân viên Booking") lẫn Ý tưởng (mục "Quản lý team") đều tái sử dụng cơ chế task này; xây trước để tránh 2 module sau làm trùng 2 phiên bản task khác nhau

---

## Giai đoạn 2 — Booking

### Module 7: Quản lý chiến dịch (Campaign)
- CRUD chiến dịch (QL Booking toàn quyền; Giám đốc/QL Ý tưởng/NV Booking/NV Ý tưởng xem)
- **Phụ thuộc:** Module 5, Module 6
- **Vì sao trước Booking & Ý tưởng:** cả Booking lẫn Ý tưởng đều có field "Campaign" tham chiếu tới module này

### Module 8: Quản lý KOL/KOC
- Database KOL/KOC đầy đủ field: tên, nền tảng, link tài khoản, follower, engagement, chủ đề/niche, giá booking, địa bàn, liên hệ, người phụ trách, lịch sử booking, đánh giá, trạng thái (Tiềm năng → Đã liên hệ → Đang deal → Đã chốt → Đang chạy → Hoàn thành)
- **Phụ thuộc:** Module 5 (người phụ trách), Module 7
- **Vì sao trước Module 9:** mỗi Booking bắt buộc phải chọn KOL/KOC đã có sẵn trong database

### Module 9: Quản lý Booking (lõi)
- CRUD Booking đầy đủ field: Client, Campaign, KOL/KOC, nội dung, deadline, ngày đăng, giá booking, chi phí, người phụ trách, danh sách nhân viên tham gia (gán nhiều nhân viên), trạng thái, file hợp đồng, file báo giá, link bài đăng, ghi chú
- Dashboard Booking: booking theo trạng thái (chờ/thương lượng/chốt/triển khai/hoàn thành/hủy), doanh thu, chi phí, lợi nhuận, công việc quá hạn, hiệu suất nhân viên booking
- **Phụ thuộc:** Module 7, Module 8
- **Vì sao trước Module 10 & 11:** cả 2 module sau đều là tính năng mở rộng gắn trực tiếp lên một booking đã tồn tại (không thể chia thù lao hay duyệt hợp đồng cho booking chưa có)

### Module 10: Chia lương/thù lao theo booking
- QL Booking gán nhiều nhân viên vào 1 booking, tự nhập số tiền thù lao cho từng người
- Hiển thị tổng phân bổ so với giá trị booking, cảnh báo nếu vượt
- Sửa lại phân bổ khi booking thay đổi, lưu lịch sử thay đổi
- Giám đốc: xem tổng quan phân bổ thù lao tất cả booking (read-only)
- NV Booking: chỉ xem phần thù lao của chính mình
- **Phụ thuộc:** Module 9
- **Vì sao trước Module 11:** đây là tính năng độc lập, không phụ thuộc luồng hợp đồng, nên làm ngay sau khi Booking lõi xong để không phải chờ

### Module 11: Luồng duyệt & ký hợp đồng booking
- Nhân viên phụ trách gửi đơn phê duyệt booking lên Giám đốc
- Giám đốc duyệt/từ chối
- Nhân viên soạn hợp đồng sau khi được duyệt, gửi lại Giám đốc
- Giám đốc tự tích ký hợp đồng trên hệ thống
- Trạng thái booking bổ sung: Chờ duyệt booking → Đã duyệt (chờ soạn hợp đồng) → Chờ Giám đốc ký hợp đồng → Đã ký (hiệu lực)
- **Phụ thuộc:** Module 9, Module 2 (quyền Giám đốc), Module 3 (ghi log từng bước)
- **Vì sao sau Module 9 & 10:** luồng này thao tác trên booking đã tồn tại và trạng thái booking cần đồng bộ với các trạng thái vận hành đã có ở Module 9

### Module 12: QL Booking thực thi trực tiếp & báo cáo
- Mở quyền cho QL Booking thực hiện các thao tác vốn của NV Booking: tìm KOL/KOC, liên hệ, gửi báo giá, thương lượng, cập nhật trạng thái
- QL Booking gửi đơn phê duyệt booking lên Giám đốc (dùng chung luồng Module 11)
- **Phụ thuộc:** Module 8, Module 11
- **Vì sao làm cuối Giai đoạn 2:** chỉ là mở rộng quyền truy cập UI đã có sẵn ở Module 8/11, nên làm sau khi UI gốc đã hoàn chỉnh và ổn định

---

## Giai đoạn 3 — Ý tưởng

### Module 13: Quản lý Ý tưởng (lõi)
- CRUD ý tưởng đầy đủ field: tên ý tưởng, người tạo, client, campaign, insight, concept, nội dung, reference, moodboard, script, deadline, người phụ trách, người review, trạng thái
- Quy trình: Ý tưởng → Review → Chỉnh sửa → Duyệt → Triển khai → Hoàn thành
- QL Ý tưởng: tạo/giao task, giao deadline, review idea, comment, yêu cầu sửa, duyệt idea, theo dõi workload, đánh giá nhân viên (dùng Module 6)
- NV Ý tưởng: tạo/sửa ý tưởng, upload reference, viết concept/script, comment, nhận task, cập nhật tiến độ, gửi review, xem feedback
- **Phụ thuộc:** Module 5, Module 6, Module 7
- **Vì sao trước Module 14 & 15:** 2 module sau đều mở rộng trên ý tưởng đã tồn tại

### Module 14: Kho ý tưởng (Idea Library)
- Phân loại theo ngành hàng, client, campaign, trend, viral, đã triển khai, chưa sử dụng
- **Phụ thuộc:** Module 13
- **Vì sao sau Module 13:** kho ý tưởng chỉ là lớp phân loại/lưu trữ trên dữ liệu ý tưởng đã có

### Module 15: Giám đốc — Sửa/Feedback/Duyệt ý tưởng
- Giám đốc xem toàn bộ ý tưởng
- Tự sửa trực tiếp nội dung ý tưởng, hoặc feedback yêu cầu chỉnh sửa → nhân viên gửi lại
- Giám đốc duyệt/từ chối ý tưởng (song song, độc lập với nút Duyệt của QL Ý tưởng)
- Nhân viên Ý tưởng xem được feedback từ cả QL Ý tưởng và Giám đốc, phân biệt rõ nguồn
- **Phụ thuộc:** Module 13, Module 2 (quyền Giám đốc), Module 3 (ghi log)
- **Vì sao làm cuối Giai đoạn 3:** đây là quyền mở rộng chồng lên quy trình duyệt gốc của QL Ý tưởng, cần quy trình gốc (Module 13) chạy ổn định trước để tránh xung đột trạng thái

---

## Giai đoạn 4 — Tổng hợp & Cấu hình (làm sau cùng vì phụ thuộc toàn bộ dữ liệu ở trên)

### Module 16: Dashboard tổng công ty
- Giám đốc: tổng nhân sự, nhân sự theo phòng ban, nhân sự mới/nghỉ việc, hiệu suất phòng ban, số booking, doanh thu booking, số chiến dịch đang chạy, số ý tưởng chờ duyệt, công việc quá hạn, báo cáo theo ngày/tháng/quý
- AdminIT: xem dashboard toàn công ty (giới hạn xem)
- **Phụ thuộc:** Module 5, 9, 10, 11, 13 (tổng hợp số liệu từ toàn bộ các module trên)

### Module 17: Báo cáo
- QL HCNS: báo cáo nhân sự, biến động nhân sự, hiệu suất nhân viên
- NV HCNS: xuất báo cáo (giới hạn xem)
- QL Booking: báo cáo booking (toàn quyền); NV Booking xem
- QL Ý tưởng: báo cáo ý tưởng (toàn quyền); NV Ý tưởng xem
- Giám đốc: toàn quyền xem tất cả báo cáo
- **Phụ thuộc:** Module 5, 9, 13 (báo cáo tổng hợp từ dữ liệu các module nghiệp vụ)

### Module 18: Cấu hình hệ thống (nghiệp vụ)
- Giám đốc cấu hình các tham số nghiệp vụ (không phải tham số kỹ thuật — phần đó thuộc AdminIT ở Module 2)
- **Phụ thuộc:** toàn bộ các module nghiệp vụ đã có, vì cấu hình thường là bật/tắt hoặc chỉnh ngưỡng cho các tính năng đã tồn tại (VD: cho phép/không cho phép vượt tổng thù lao booking)

---

## Tóm tắt thứ tự làm

```
Giai đoạn 0 (Nền tảng):     1 → 2 → 3
Giai đoạn 1 (Dữ liệu nền):  4 → 5 → 6
Giai đoạn 2 (Booking):      7 → 8 → 9 → 10 → 11 → 12
Giai đoạn 3 (Ý tưởng):      13 → 14 → 15
Giai đoạn 4 (Tổng hợp):     16 → 17 → 18
```

Lưu ý: Giai đoạn 2 và Giai đoạn 3 (Booking, Ý tưởng) đều chỉ phụ thuộc Giai đoạn 0 + 1, **không phụ thuộc lẫn nhau** — có thể làm song song 2 đội nếu cần rút ngắn thời gian, miễn Giai đoạn 0 + 1 đã xong trước.
