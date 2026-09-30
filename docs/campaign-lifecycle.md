# Luồng chiến dịch — cập nhật 27/09/2026

| Bước | Người thao tác | Kết quả |
|---|---|---|
| Tạo chiến dịch | QL Booking | Đang chờ ký (`planning`) |
| Chốt chiến dịch | Giám đốc | Đang chạy (`running`); mở cho tạo Booking/Ý tưởng |
| Đánh dấu Hoàn thành | QL Booking | Hoàn thành (`completed`); chưa xác nhận nhận tiền |
| Tích “Tôi xác nhận đã nhận được tiền”, bấm Nghiệm thu | QL Booking | Đã nghiệm thu (`accepted`) |

- Form tạo/sửa hiển thị trạng thái chỉ đọc. Thao tác Hoàn thành/Nghiệm thu nằm ở danh sách và chi tiết chiến dịch, chỉ xuất hiện đúng bước/role.
- Hoàn thành là QL xác nhận công việc xong; không tự hoàn thành các Booking/ý tưởng/task con. Không bổ sung điều kiện “tất cả Booking hoàn thành” khi chưa được yêu cầu.
- Nghiệm thu chiến dịch là xác nhận nhận tiền do QL nhập, không phải hệ thống tự xác minh giao dịch ngân hàng. Tách biệt nghiệm thu từng Booking và đối soát chi trả cast/hoa hồng; không tự đánh dấu các khoản chi đã thanh toán.
- Lưu `CompletedAt`, `CompletedByUserId`, `AcceptedAt`, `AcceptedByUserId`; audit `campaign_completed`, `campaign_accepted` cùng transaction lưu trạng thái.
- Chặn nhảy bước, sai role, thiếu xác nhận tiền, thiếu CSRF. Yêu cầu gửi lặp không đổi mốc thời gian, không tạo trùng audit. Trạng thái là concurrency token, tránh ghi đè bằng dữ liệu cũ.
- Sau hoàn thành không được hủy; sau nghiệm thu không được sửa hoặc hủy. Không có thao tác tự mở lại. Dữ liệu lịch sử hủy/tạm dừng vẫn đọc được.

## Database và kiểm tra

- Migration: `20260927160132_CampaignCompletionAndReceipt`, đã áp dụng local. Bổ sung bốn trường nullable và giá trị accepted vào CHECK constraint, không tự nghiệm thu dữ liệu cũ.
- Có backup COPY_ONLY đã VERIFYONLY trước migration, từ lượt test clone: `HanaMedia_V4_Test_c92c92d0c95c475d86d3330a31e15275_source.bak` trong thư mục SQL Server Backup.
- Down chặn khi đã có lịch sử hoàn thành/nghiệm thu, tránh làm mất dấu xác nhận nhận tiền.
- `tests/V4Flow --ui`: **121 kiểm tra đạt**, trong đó có bước chạy **38 kiểm tra trình duyệt desktop/mobile**. Dữ liệu nghiệp vụ kiểm thử nằm trên SQL clone đã dọn; source giữ nguyên 7 chiến dịch và chưa bị đánh dấu nghiệm thu mẫu nào.
- Đã kiểm tra tạo giả mạo dấu hoàn thành/nhận tiền, chuyển đúng bước, quyền QL, CSRF, thiếu checkbox, bấm lặp, audit, khóa sau nghiệm thu, stale update, bộ lọc accepted, thao tác bấm Hoàn thành/Nghiệm thu và giao diện mobile.

## Bố tự thử

- QL Booking: chiến dịch demo #13 đang chạy có nút **Đánh dấu Hoàn thành**.
- Sau khi hoàn thành, xuất hiện checkbox xác nhận nhận tiền và nút **Nghiệm thu**.
- Hoặc tạo chiến dịch mới → dùng Giám đốc chốt → quay lại QL Booking để thử đủ bốn bước.
