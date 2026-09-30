# Chọn nhân viên và nhập thù lao Booking — 30/09/2026

## Sử dụng

- Form **Tạo Booking** cho phép chọn nhiều nhân viên và nhập số tiền riêng cho từng người. Có thể để trống và phân bổ sau.
- Tìm theo tên có/không dấu hoặc mã nhân viên; kết hợp bộ lọc phòng ban và “Chỉ người đã chọn”. Lọc chỉ thay đổi hiển thị, không làm mất người hoặc số tiền đã chọn.
- Hiển thị quỹ hoa hồng, tổng chia nhân viên và phần QL còn lại; cập nhật khi đổi giá Booking, chọn người hoặc sửa số tiền. Số tiền có dòng định dạng VNĐ để dễ đọc.
- Trang **Chi tiết** dùng cùng giao diện để thay đổi phân bổ. Bỏ chọn và lưu sẽ gỡ người đó cùng khoản chia. Form sửa thông tin Booking không ghi đè các phân bổ hiện có.
- Danh sách có vùng cuộn giới hạn chiều cao, bố cục một cột trên mobile; không thêm hoặc thay đổi sidebar.

## Quy tắc được giữ nguyên

- Nhân viên đang làm việc/thử việc ở tất cả phòng ban được chọn, không giới hạn role hay yêu cầu có tài khoản đăng nhập. Nhân sự ngừng hoạt động không được thêm mới. Nhận thù lao không tự cấp quyền truy cập/chỉnh sửa Booking.
- Quỹ theo tỷ lệ cấu hình khi tạo và snapshot đã lưu của Booking khi chỉnh phân bổ. Không tự chia đều, không cộng trùng thù lao nhân viên vào quỹ.
- Server kiểm tra số tiền hợp lệ, không âm, tối đa hai chữ số thập phân, tổng không vượt quỹ và nhân viên đủ điều kiện. Booking và phân bổ ban đầu được lưu cùng transaction; lỗi phân bổ không tạo Booking dở dang.
- Ghi audit khi phân bổ lúc tạo. Giữ nguyên quyền QL phụ trách, CSRF, chặn thay đổi sau thanh toán/hủy và quy trình phê duyệt.
- Dùng `BookingWages` hiện có, không thay schema/migration.

## Kiểm thử

- `dotnet build --configuration Release --no-restore`.
- Bộ `tests/V4Flow --ui` chạy trên SQL clone riêng: kiểm tra tạo không phân bổ, tạo kèm tiền lẻ, vượt quỹ, âm, quá hai số lẻ, sai định dạng, mã nhân viên không tồn tại, nhân sự ngừng hoạt động, transaction, audit; nhận thù lao liên phòng ban, không có tài khoản, bảng đối soát và giữ nguyên quyền truy cập; thao tác trình duyệt chọn nhiều người, tìm không dấu, lọc phòng ban, giữ lựa chọn khi lọc, sửa/gỡ khoản chia, desktop và mobile.
- Ảnh kiểm tra trong thư mục ignored `tests/artifacts/v4/booking-create-wages-*.png`; dữ liệu test không ghi vào database vận hành.
- Kết quả sau mở rộng toàn bộ phòng ban: build 0 lỗi (6 cảnh báo có sẵn); 148 kiểm tra tích hợp đạt và 62 kiểm tra viewport/tương tác trình duyệt đạt, không có lỗi JavaScript trang. SQL clone được dọn sau kiểm thử; backup nguồn được giữ lại.
