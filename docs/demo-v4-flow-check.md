# Kiểm tra luồng bằng dữ liệu demo — 27/09/2026

## Kết quả

- 96 kiểm tra HTTP/database đạt trên bộ dữ liệu demo giữ lại trong DB local HanaMedia.
- 62 kiểm tra trình duyệt đạt: đúng nút thao tác theo vai trò/trạng thái, không lỗi JavaScript; 5 vai trò ở desktop 1440px và mobile 390px, sidebar và menu mobile hoạt động.
- Đây là kiểm tra luồng v4 với các ca đã chạy, không phải cam kết không còn lỗi trong mọi tình huống tải/concurrency/production.
- Booking lịch sử #4 vẫn giữ nguyên giá trị 10 triệu, chi phí 0, trạng thái `da_duyet`; không tự chuyển số liệu lịch sử sang công thức mới.
- Có bản sao lưu SQL COPY_ONLY đã VERIFYONLY trước khi tạo. Đường dẫn chính xác nằm trong manifest local.

## Bộ demo để tự thử

- 10 tài khoản `demo_v4_*`, mỗi tài khoản liên kết một hồ sơ nhân viên; không thay mật khẩu tài khoản sẵn có.
- 3 chiến dịch: #13 đang chạy và đã chốt; #14 và #15 **Đang chờ ký**. Theo yêu cầu mới nhất 27/09/2026, Giám đốc chốt mới tự chuyển Đang chạy; giữ nguyên tên demo cũ.
- 3 KOL giả lập; không tạo tài khoản đăng nhập cho KOL.
- Ý tưởng #36: creator nội bộ, chờ quản lý review. #37: creator đối tác, quản lý đã review duyệt.

| Booking | Bước giữ lại để thử |
|---|---|
| #5 | Nháp; chia thù lao / gửi duyệt |
| #6 | Chờ Giám đốc duyệt hoặc từ chối |
| #7 | Giám đốc đã từ chối; sửa và gửi lại |
| #8 | Đã duyệt; chờ soạn/tải hợp đồng |
| #9 | Chờ Pháp lý kiểm tra |
| #10 | Pháp lý trả sửa, có lý do |
| #11 | Pháp lý đã duyệt; chờ Giám đốc tích xác nhận ký |
| #12 | Đã ký và đang triển khai; chờ nghiệm thu |
| #13 | Hoàn thành; đã đánh dấu trả phần QL, còn khoản khác chưa trả |

Mỗi Booking 100 triệu: công ty 50 triệu, cast 40 triệu, hoa hồng 10 triệu. NV 1 nhận 2 triệu, NV 2 nhận 1 triệu, QL còn 7 triệu. Cast chia ba KOL, tổng sau làm tròn vẫn đúng 40 triệu.

Booking #13 đã đi qua vòng Pháp lý trả sửa → NV được phân công gửi phiên bản mới → Pháp lý duyệt → Giám đốc ký → triển khai → nghiệm thu → Kế toán đối soát. Các bước có audit log; đánh dấu đối soát không chuyển tiền thật.

## Các chặn đã kiểm tra

- QL không được chốt chiến dịch thay Giám đốc; Ý tưởng không thấy chiến dịch chưa chốt.
- Không tạo Booking trên chiến dịch tạm dừng; không chia vượt quỹ hoa hồng.
- Không ký vượt bước Pháp lý; không duyệt phiên bản hợp đồng cũ.
- Giao diện chỉ hiện thao tác phù hợp: mẫu đã hoàn thành không còn tải lại hợp đồng/nghiệm thu.
- Kế toán không sửa Booking; QL không vào Kế toán; Giám đốc chỉ xem, không đánh dấu thanh toán.
- Hồ sơ nhận lương chỉ cho bản thân, Giám đốc, QL/NV HCNS; các vai trò thử còn lại bị chặn.

## Lưu ý khi bố tự thử

- Tài khoản, mật khẩu ngẫu nhiên, ID và checklist chi tiết: `tests/artifacts/demo-v4-access.md` (Git bỏ qua). Không đưa mật khẩu lên Git.
- Dữ liệu demo làm tăng tổng dashboard/báo cáo. Cần xử lý bộ demo trước khi dùng số tổng cho vận hành thật; hiện không tự xóa vì cần để tự test.
- Luồng mới: QL Booking tạo → Đang chờ ký → Giám đốc chốt → tự chuyển Đang chạy, mở cho Booking và Ý tưởng. QL không sửa trực tiếp trạng thái. Content Creator vẫn dùng cách A: nội bộ hoặc tham chiếu KOL, không cấp tài khoản KOL.
- Nút hành động ở bảng ký hợp đồng desktop còn khá chật khi một Booking có nhiều KOL/tên dài; không chặn luồng. Mobile bảng rộng cuộn ngang trong khung.
- Chứng từ DOCX đính kèm ghi rõ DEMO, không phải hợp đồng/nghiệm thu sử dụng thực tế.

## Công cụ kiểm tra

- `planning` = Đang chờ ký, `running` = Đang chạy. Form hiển thị trạng thái chỉ đọc; Create luôn ghi `planning`, Edit bỏ qua trường trạng thái từ client; chỉ Confirm của Giám đốc ghi `running` cùng dấu chốt. Ký hợp đồng Booking vẫn là bước riêng, không tự ký khi chốt chiến dịch.
- Migration dữ liệu `20260927090000_CampaignConfirmationStartsRunning` đã áp dụng: #9 và #14 từ running nhưng chưa chốt về planning; có audit và backup trước khi chạy. Giữ nguyên 10 Booking, bằng chứng chốt, chiến dịch đã hủy và lịch sử hoàn thành. Down không đảo dữ liệu nghiệp vụ; phục hồi chính xác bằng backup nếu cần.
- `tests/campaign-status-ui.cjs`: kiểm tra chỉ đọc trên web thật (form readonly, tạo mặc định chờ ký, nút chốt đúng role, bộ lọc, chi tiết, lựa chọn Campaign trong Booking và mobile).
- `tests/V4Flow`: lượt kiểm tra sau sửa luồng đạt **91 ca** trên DB clone, gồm tạo/sửa giả mạo trạng thái, dấu chốt giả, chốt tự chạy, chốt lặp không đổi thời điểm, sửa không hủy dấu chốt và toàn bộ hồi quy Booking/Pháp lý/Kế toán. DB clone đã được dọn, nguồn không chứa dữ liệu test của lượt này.

- `tests/DemoV4`: công cụ tạo demo một lần, cần đối số `--seed-local-demo`, chỉ cho đúng server/database local. Từ chối nếu đã có prefix hoặc manifest; không chạy lại để reset dữ liệu.
- `tests/demo-v4-ui.cjs`: đọc manifest demo, đăng nhập và kiểm tra giao diện; không gửi form thay đổi nghiệp vụ. Ảnh và kết quả ở `tests/artifacts/demo-v4-ui/` (Git bỏ qua).
