# Dashboard Giám đốc: thù lao và công nợ

Cập nhật 28/09/2026, chỉ bổ sung cho role Giám đốc, không thêm vào dashboard Admin IT.

## Cách tính

- **Thù lao Booking**: tổng quỹ hoa hồng của Booking tạo trong kỳ ngày/tháng/quý, tính theo `CommissionPercent` đã lưu trên từng Booking v4. Bao gồm phần QL và nhân viên, không cộng lại các dòng chia thù lao.
- **Thù lao KOL/KOC**: tổng quỹ cast theo `CastPercent` đã lưu trên từng Booking v4 trong cùng kỳ. Cả hai quỹ loại Booking đã hủy, làm tròn từng Booking trước khi cộng. Đây không phải tổng đã thanh toán.
- Booking lịch sử chưa có snapshot tài chính v4 không được tự áp tỷ lệ cấu hình hiện tại; dashboard ghi rõ số bản ghi chưa tính.
- **Công nợ**: số lượng và danh sách chiến dịch chưa nghiệm thu, loại chiến dịch đã hủy. Gồm chờ ký, đang chạy, hoàn thành và dữ liệu lịch sử chưa nghiệm thu; tính tại thời điểm hiện tại, không bị giới hạn bởi kỳ dashboard.
- Khi QL Booking xác nhận đã nhận tiền và nghiệm thu, chiến dịch tự rời danh sách công nợ ở lần tải tiếp theo. Không quy đổi ngân sách chiến dịch thành tiền phải thu khi chưa có quy tắc nghiệp vụ này.
- Dashboard xem trước tối đa 20 chiến dịch, ưu tiên hoàn thành rồi đang chạy. Liên kết danh sách đầy đủ giữ bộ lọc `pendingAcceptance=true`, có đường dẫn xem chi tiết từng chiến dịch.

## Kiểm chứng

- Không thay schema, không cần migration mới cho phần dashboard này.
- Release build thành công, 0 lỗi; 6 cảnh báo có sẵn.
- `dotnet run --project tests/V4Flow/V4Flow.csproj --configuration Release --no-build -- --ui`: **135 kiểm tra đạt**, gồm **44 kiểm tra trình duyệt desktop/mobile**, không có lỗi JavaScript trang.
- Đã kiểm tra tỷ lệ lưu từng Booking, loại Booking hủy/legacy, công nợ tồn từ kỳ trước, giới hạn xem trước, nghiệm thu giảm công nợ, bộ lọc danh sách đầy đủ và không thêm các mục mới vào Admin IT.
- Dữ liệu test chạy trên SQL clone riêng và đã dọn; không nghiệm thu hay tạo fixture vào database nguồn. Ảnh QA nằm trong thư mục ignored `tests/artifacts/v4/`.
