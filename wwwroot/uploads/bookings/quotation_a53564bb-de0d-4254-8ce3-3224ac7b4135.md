# Danh sách Module – Web App Quản lý Nhân sự HanaMedia

> Tổng hợp từ tài liệu yêu cầu (requirement_HanaMedia.md). 14 module, bám sát nội dung gốc, không thêm/bớt.

---

## 1. Module Xác thực & Bảo mật truy cập
- Đăng nhập/đăng xuất
- Kiểm soát truy cập theo IP nội bộ (whitelist dải IP văn phòng, chặn ở tầng server/firewall)
- Thông báo riêng khi truy cập từ mạng ngoài (khác với lỗi sai tài khoản/mật khẩu)
- Ghi nhận các lần truy cập bị chặn từ mạng ngoài vào Audit log

## 2. Module Quản lý tài khoản & Phân quyền (AdminIT)
- Tạo tài khoản
- Khóa/mở tài khoản
- Phân quyền, thay đổi role
- Reset mật khẩu
- Xem lịch sử đăng nhập (thời gian đăng nhập/đăng xuất, thiết bị, IP)

## 3. Module Audit Log (Nhật ký hệ thống)
- Ghi log thao tác của tất cả role (tạo/sửa/xóa/duyệt/đăng nhập...) trên mọi module (Nhân sự, Booking, Ý tưởng, Tài khoản)
- Bộ lọc log: theo người dùng, loại hành động, module, khoảng thời gian
- Xuất báo cáo log
- Cảnh báo hành vi bất thường (đăng nhập thất bại nhiều lần, xóa hàng loạt dữ liệu)
- Giám đốc xem báo cáo tổng quan (không xem chi tiết); AdminIT xem toàn bộ chi tiết

## 4. Module Dashboard
- Dashboard toàn công ty (Giám đốc xem, AdminIT chỉ xem): tổng nhân sự, nhân sự theo phòng ban, nhân sự mới/nghỉ việc, hiệu suất phòng ban, số booking, doanh thu booking, số chiến dịch đang chạy, số ý tưởng chờ duyệt, công việc quá hạn, báo cáo ngày/tháng/quý
- Dashboard Booking (QL Booking): booking theo trạng thái (chờ/thương lượng/chốt/triển khai/hoàn thành/hủy), doanh thu, chi phí, lợi nhuận, công việc quá hạn, hiệu suất nhân viên booking

## 5. Module Quản lý Nhân sự (HCNS)
- CRUD hồ sơ nhân viên (thêm/sửa/xóa-ngừng hoạt động/chuyển phòng ban/đổi chức vụ/đổi trạng thái)
- Thông tin nhân viên: họ tên, avatar, ngày sinh, SĐT, email, địa chỉ, ngày vào công ty, phòng ban, chức vụ, người quản lý, loại hợp đồng, trạng thái
- Cập nhật giấy tờ, theo dõi hợp đồng
- Xem lịch sử làm việc, chức vụ, phòng ban, mức lương/phụ cấp (nếu có), đánh giá hiệu suất, lịch sử khen thưởng/kỷ luật (Giám đốc xem)

## 6. Module Quản lý Phòng ban
- Quản lý phòng ban (thuộc quyền Giám đốc theo ma trận phân quyền)

## 7. Module Booking / KOL / Đối tác
- Database KOL/KOC: tên, nền tảng, link tài khoản, follower, engagement, chủ đề/niche, giá booking, địa bàn, liên hệ, người phụ trách, lịch sử booking, đánh giá, trạng thái (Tiềm năng → Đã liên hệ → Đang deal → Đã chốt → Đang chạy → Hoàn thành)
- Quản lý Booking: client, campaign, KOL/KOC, nội dung, deadline, ngày đăng, giá booking, chi phí, người phụ trách, danh sách nhân viên tham gia, trạng thái, file hợp đồng, file báo giá, link bài đăng, ghi chú

## 8. Module Chia lương/thù lao theo Booking
- Gán nhiều nhân viên vào 1 booking
- Nhập/tùy chỉnh số tiền thù lao riêng cho từng nhân viên
- Hiển thị tổng đã phân bổ so với giá trị booking, cảnh báo nếu vượt
- Chỉnh sửa lại phân bổ khi booking thay đổi
- Lưu lịch sử thay đổi phân bổ (phục vụ audit)
- Nhân viên chỉ xem được phần thù lao của chính mình

## 9. Module Quản lý Chiến dịch (Campaign)
- Chức năng riêng trong ma trận phân quyền (QL Booking toàn quyền; Giám đốc, NV Booking, QL/NV Ý tưởng chỉ xem)

## 10. Module Quản lý Ý tưởng (Idea)
- Quy trình: Ý tưởng → Review → Chỉnh sửa → Duyệt → Triển khai → Hoàn thành
- Thông tin ý tưởng: tên, người tạo, client, campaign, insight, concept, nội dung, reference, moodboard, script, deadline, người phụ trách, người review, trạng thái
- Comment, yêu cầu sửa, duyệt idea
- Idea Library: theo ngành hàng, theo client, theo campaign, theo trend, idea viral, idea đã triển khai, idea chưa sử dụng

## 11. Module Phân công công việc & Quản lý team
- Tạo task, giao task, giao deadline
- Theo dõi tiến độ/workload
- Đánh giá nhân viên, xem KPI
- Áp dụng cho QL HCNS, QL Booking, QL Ý tưởng (theo ma trận phân quyền)
- Trạng thái task (Ý tưởng): To do → In Progress → Review → Need Revision → Approved → Done (nhân viên không được tự chuyển sang Approved)

## 12. Module Duyệt công việc (Approval)
- Giám đốc: duyệt tăng/giảm lương, nghỉ việc, booking/chiến dịch quan trọng, ý tưởng/campaign quan trọng
- QL Booking: duyệt công việc thuộc Booking
- QL Ý tưởng: duyệt ý tưởng

## 13. Module Báo cáo
- Báo cáo nhân sự, biến động nhân sự, hiệu suất nhân viên (HCNS)
- Báo cáo Booking (QL/NV Booking)
- Báo cáo Ý tưởng (QL/NV Ý tưởng)
- Báo cáo tổng hợp (Giám đốc)

## 14. Module Cấu hình hệ thống
- Cấu hình nghiệp vụ (Giám đốc toàn quyền)
- Cấu hình kỹ thuật (AdminIT: giới hạn dung lượng upload, thời gian hết hạn phiên đăng nhập...)

---

## Ghi chú
- Danh sách trên bám sát 100% nội dung tài liệu `requirement_HanaMedia.md`, không tự thêm chức năng ngoài mô tả (VD: không thêm chấm công/nghỉ phép vì tài liệu chỉ nói loại bỏ khỏi phần Giám đốc, không mô tả nghiệp vụ cụ thể ở nơi khác).
- Mục "11. Tổng hợp các thay đổi" trong tài liệu gốc là changelog, không phải chức năng nghiệp vụ nên không tách thành module riêng.
