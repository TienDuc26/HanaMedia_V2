# Prompt gửi Antigravity — Cập nhật Frontend theo Requirement mới (HanaMedia)

Tôi đính kèm file `requirement_HanaMedia_v2.md` — đây là bản requirement **mới nhất, đã chốt**, mô tả đầy đủ phân quyền và nghiệp vụ hiện tại của hệ thống. Hãy đọc kỹ toàn bộ file trước khi làm bất cứ việc gì.

## Bối cảnh
Đây **không phải** dự án làm mới từ đầu. Codebase và database hiện tại đã có sẵn phần lớn chức năng. Nhiệm vụ của bạn là **cập nhật frontend cho đúng những phần đã thay đổi/thêm/bớt** so với logic hiện có trong code, dựa trên các điểm khác biệt được liệt kê ở mục "Phạm vi thay đổi bắt buộc" bên dưới. **Không** viết lại, không refactor, không tạo mới các phần không nằm trong phạm vi này.

## Nguyên tắc bắt buộc (không được vi phạm)
1. **Không thêm UI/tính năng ngoài yêu cầu.** Chỉ implement đúng những gì được liệt kê trong "Phạm vi thay đổi bắt buộc". Không tự sáng tạo thêm màn hình, nút bấm, filter, hay field nào khác dù có vẻ "hợp lý".
2. **Phải khớp với dữ liệu đã có trong DB.** Trước khi dựng UI cho bất kỳ field/thông tin nào, kiểm tra schema/model hiện tại xem field đó đã tồn tại chưa và tên gọi/kiểu dữ liệu chính xác là gì. Nếu field cần thiết **chưa có trong DB**, liệt kê rõ ra (tên bảng, tên field đề xuất, kiểu dữ liệu, quan hệ) và **hỏi lại tôi để xác nhận trước khi tạo migration/model mới** — không tự ý thêm.
3. **Đồng bộ với UI/UX hiện tại.** Dùng lại đúng component, style, layout, pattern (form, table, modal, badge trạng thái, phân trang, toast...) đang có trong codebase. Không đưa vào thư viện UI mới, không đổi design system.
4. **Đồng bộ phân quyền (RBAC).** Mọi màn hình/nút hành động mới phải tuân đúng ma trận phân quyền ở mục 1 của file requirement — ẩn/khoá đúng theo role, không hiển thị nhầm cho role không có quyền.
5. **Trước khi code**, hãy quét codebase hiện tại (routes, pages/components liên quan đến Booking, Ý tưởng, Giám đốc, HCNS) và tóm tắt lại ngắn gọn: cấu trúc hiện có, các component/API đã dùng cho các màn hình liên quan, để tôi xác nhận hiểu đúng trước khi bạn sửa.
6. Nếu một thay đổi trong requirement cần sửa cả API/backend logic để frontend hoạt động đúng, hãy chỉ ra rõ những chỗ backend cần điều chỉnh (không tự ý sửa nếu ngoài phạm vi bạn được giao — chỉ liệt kê để tôi biết).

## Phạm vi thay đổi bắt buộc (chỉ làm đúng các mục này)

### A. Booking — Chia lương/thù lao (giữ nguyên như logic gốc, KHÔNG qua Giám đốc duyệt)
- Màn hình chia thù lao theo booking vẫn thuộc **QL Booking**: QL Booking gán nhiều nhân viên vào 1 booking và tự nhập số tiền thù lao cho từng người, không có bước gửi đề xuất/chờ Giám đốc duyệt.
- Hiển thị tổng số tiền đã phân bổ so với giá trị booking, cảnh báo nếu vượt.
- Cho phép sửa lại phân bổ khi booking thay đổi (thêm/bớt người, đổi số tiền); lưu lịch sử thay đổi.
- Giám đốc: thêm màn hình/tab **xem tổng quan** phân bổ thù lao của tất cả booking (read-only, không có nút sửa).
- NV Booking: chỉ xem được phần thù lao của chính mình trên từng booking đã tham gia.
- ⚠️ Nếu trong code hiện tại đã tồn tại luồng "đề xuất → Giám đốc duyệt" cho phần này (do một bản requirement trước đó), hãy **gỡ bỏ bước duyệt đó**, trả lại luồng nhập trực tiếp một bước như mô tả ở trên.

### B. Booking — Luồng duyệt & ký hợp đồng (tính năng mới)
- Nhân viên phụ trách booking (NV Booking hoặc QL Booking) hoàn tất thông tin booking → **gửi đơn phê duyệt booking** lên Giám đốc.
- Giám đốc có màn hình xem danh sách đơn chờ duyệt → **duyệt hoặc từ chối**.
- Sau khi duyệt, nhân viên phụ trách **soạn hợp đồng** (dựa trên booking đã duyệt) → gửi lại cho Giám đốc.
- Giám đốc xem hợp đồng → **tự tích ký (xác nhận ký)** ngay trên hệ thống.
- Trạng thái booking cần phản ánh đúng luồng: `Chờ duyệt booking → Đã duyệt (chờ soạn hợp đồng) → Chờ Giám đốc ký hợp đồng → Đã ký (hiệu lực)`, hiển thị đúng badge/trạng thái tương ứng trên các màn hình liên quan (dashboard booking, chi tiết booking).
- NV Booking/QL Booking: không được có nút "ký hợp đồng" — chỉ Giám đốc mới thấy nút này.

### C. Booking — QL Booking thực hiện thêm tác vụ thực thi
- Màn hình/khu vực thao tác của QL Booking (tìm KOL, liên hệ, gửi báo giá, thương lượng) hiện chỉ dành cho NV Booking — nay cần **mở thêm quyền/thao tác này cho QL Booking** (dùng lại đúng UI hiện có của NV Booking, không tạo bản UI riêng).
- QL Booking cần thấy nút/luồng "Gửi đơn phê duyệt booking lên Giám đốc" giống mục B.

### D. Ý tưởng — Giám đốc sửa/feedback/duyệt trực tiếp (tính năng mới)
- Trên màn hình chi tiết ý tưởng, thêm quyền cho role Giám đốc:
  - Nút **chỉnh sửa trực tiếp** nội dung ý tưởng (Insight, Concept, Nội dung, Script...) — dùng lại đúng form edit hiện có của QL Ý tưởng/NV Ý tưởng, chỉ mở quyền truy cập cho Giám đốc.
  - Khu vực **feedback/comment** (nếu đã có comment thread cho ý tưởng, dùng lại; nếu chưa có, báo lại tôi trước khi tạo mới).
  - Hai nút **Duyệt / Từ chối** dành riêng cho Giám đốc trên ý tưởng, độc lập với nút Duyệt idea hiện có của QL Ý tưởng (không gộp chung, vì đây là 2 luồng song song).
- Nhân viên Ý tưởng: màn hình xem feedback cần hiển thị được cả feedback từ QL Ý tưởng lẫn từ Giám đốc (phân biệt rõ nguồn feedback), và có nút "Gửi lại" sau khi chỉnh sửa theo feedback của Giám đốc.
- Đảm bảo quyền: NV Ý tưởng, QL Ý tưởng **không** thấy nút Duyệt/Từ chối của Giám đốc.

## Việc cần làm theo thứ tự
1. Đọc file requirement đính kèm.
2. Quét codebase hiện tại, xác định các file/component/API liên quan đến 4 nhóm A, B, C, D ở trên.
3. Đối chiếu schema DB hiện có với dữ liệu cần cho từng mục; liệt kê field/bảng còn thiếu (nếu có) và hỏi xác nhận trước khi migrate.
4. Trình bày lại cho tôi một bản tóm tắt kế hoạch thay đổi (file nào sửa, field nào thêm nếu có, component nào tái sử dụng) **trước khi bắt đầu code**.
5. Sau khi tôi xác nhận, mới tiến hành sửa code.
6. Sau khi hoàn tất, đối chiếu lại với ma trận phân quyền ở mục 1 của file requirement để đảm bảo không role nào bị thừa/thiếu quyền trên UI.
