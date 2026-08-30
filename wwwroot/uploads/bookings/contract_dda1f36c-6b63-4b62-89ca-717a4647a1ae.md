# Yêu cầu hệ thống – Web App Quản lý Nhân sự HanaMedia

## 1. Ma trận phân quyền tổng quan

| Chức năng | Giám đốc | AdminIT | QL HCNS | NV HCNS | QL Booking | NV Booking | QL Ý tưởng | NV Ý tưởng |
|---|---|---|---|---|---|---|---|---|
| Xem dashboard toàn công ty | ✅ | 👁️ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Quản lý nhân sự | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Quản lý phòng ban | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Booking/KOL/đối tác | 👁️ | ❌ | ❌ | ❌ | ✅ | 👁️ | ❌ | ❌ |
| Chia lương/thù lao theo booking | 👁️ (xem tổng quan) | ❌ | ❌ | ❌ | ✅ | 👁️ (chỉ xem phần của mình) | ❌ | ❌ |
| Duyệt & ký hợp đồng booking | ✅ | ❌ | ❌ | ❌ | 👁️ (gửi đề xuất) | 👁️ (soạn hợp đồng theo phân công) | ❌ | ❌ |
| Quản lý chiến dịch | 👁️ | ❌ | ❌ | ❌ | ✅ | 👁️ | 👁️ | 👁️ |
| Quản lý ý tưởng | ✅ (nội dung: sửa/feedback/duyệt/từ chối) | ❌ | ❌ | ❌ | 👁️ | 👁️ | ✅ | ✅ |
| Phân công công việc | ✅ | ❌ | ✅ | ❌ | ✅ | ❌ | ✅ | ❌ |
| Duyệt công việc | ✅ | ❌ | ❌ | ❌ | ✅ | ❌ | ✅ | ❌ |
| Báo cáo | ✅ | ❌ | ✅ | 👁️ | ✅ | 👁️ | ✅ | 👁️ |
| Cấu hình hệ thống (nghiệp vụ) | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Quản lý tài khoản người dùng | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Xem nhật ký hệ thống (Audit log) | 👁️ (tổng quan) | ✅ (chi tiết, toàn bộ) | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |

- ✅ = toàn quyền
- 👁️ = chỉ xem / quyền hạn giới hạn
- ❌ = không có quyền

---

## 2. Role Giám đốc

Đây là **role quản trị cấp cao nhất**, không trực tiếp xử lý từng công việc hằng ngày mà tập trung vào **tình hình nhân sự, doanh thu/công việc, hiệu suất và phê duyệt**.

### Dashboard
Giám đốc có thể xem:
- Tổng số nhân sự hiện tại
- Nhân sự theo phòng ban
- Nhân sự mới/nghỉ việc
- Hiệu suất từng phòng ban
- Số lượng booking
- Doanh thu booking
- Số chiến dịch đang chạy
- Số ý tưởng đang chờ duyệt
- Công việc quá hạn
- Báo cáo theo ngày/tháng/quý

### Quản lý nhân sự
Giám đốc có thể:
- Xem toàn bộ hồ sơ nhân viên
- Xem lịch sử làm việc
- Xem chức vụ
- Xem phòng ban
- Xem mức lương/phụ cấp nếu hệ thống có quản lý
- Xem đánh giá hiệu suất
- Xem lịch sử khen thưởng/kỷ luật

### Phê duyệt
Có thể phê duyệt những vấn đề quan trọng:
- Tăng/giảm lương
- Nghỉ việc
- Booking/chiến dịch quan trọng
- Ý tưởng/campaign quan trọng

### Ý tưởng – Sửa, Feedback & Duyệt
Giám đốc có thể:
- Xem toàn bộ ý tưởng của các phòng/nhóm, không giới hạn theo người phụ trách
- **Tự chỉnh sửa trực tiếp** nội dung ý tưởng (Insight, Concept, Nội dung, Script...) nếu muốn
- Hoặc để lại **feedback** trên từng ý tưởng, nêu rõ cần sửa gì; nhân viên/QL Ý tưởng chỉnh sửa theo feedback rồi **gửi lại** cho Giám đốc
- Sau khi xem lại, Giám đốc **duyệt hoặc từ chối** ý tưởng đó
- Quy trình duyệt/từ chối của Giám đốc áp dụng song song với quy trình Review nội bộ của QL Ý tưởng — thường dùng cho các ý tưởng/campaign quan trọng cần Giám đốc thông qua trước khi triển khai (xem thêm mục "Phê duyệt")
- Mọi lượt sửa, feedback, gửi lại, duyệt/từ chối của Giám đốc trên ý tưởng đều được ghi lại trong Audit log

### Chia lương/thù lao theo booking
Chức năng này **vẫn thuộc quyền QL Booking** như bản gốc: QL Booking là người vừa chia việc (gán nhân viên vào booking) vừa chia thù lao (nhập số tiền cho từng người), không cần trình Giám đốc duyệt riêng. Giám đốc chỉ xem tổng quan để theo dõi (xem chi tiết ở mục 6 – Role Quản lý Booking).

### Luồng duyệt & ký hợp đồng booking
Đây là luồng nghiệp vụ mới được bổ sung:

1. Nhân viên phụ trách booking (NV Booking, hoặc QL Booking khi trực tiếp thực hiện) hoàn tất thông tin booking (Client, KOL/KOC, nội dung, giá booking...) và **gửi đơn phê duyệt booking** lên Giám đốc
2. Giám đốc xem xét và **duyệt hoặc từ chối** đơn phê duyệt booking đó
3. Sau khi được duyệt, nhân viên phụ trách **soạn hợp đồng** dựa trên thông tin booking đã duyệt, rồi **gửi lại hợp đồng** cho Giám đốc
4. Giám đốc xem lại hợp đồng và **tự tích ký (xác nhận ký)** hợp đồng ngay trên hệ thống
5. Hợp đồng sau khi Giám đốc ký được coi là chính thức có hiệu lực; toàn bộ các bước (gửi đơn, duyệt, gửi hợp đồng, ký) đều được ghi lại trong Audit log

Trạng thái booking gợi ý bổ sung cho luồng này:
`Chờ duyệt booking → Đã duyệt (chờ soạn hợp đồng) → Chờ Giám đốc ký hợp đồng → Đã ký (hiệu lực)`
— song song với các trạng thái vận hành hiện có (Đang triển khai, Hoàn thành, Hủy...).

### Giám sát hệ thống
- Xem báo cáo tổng quan về hoạt động hệ thống (số lượng đăng nhập, cảnh báo bất thường do AdminIT cung cấp)
- Không thao tác trực tiếp vào log chi tiết — phần này thuộc quyền AdminIT

---

## 3. Role AdminIT

Đây là role **quản trị kỹ thuật hệ thống**, chịu trách nhiệm về tài khoản, phân quyền và giám sát toàn bộ hoạt động trên hệ thống. AdminIT không tham gia vào nghiệp vụ nhân sự/booking/ý tưởng.

### Quản lý tài khoản
- Tạo tài khoản
- Khóa/mở tài khoản
- Phân quyền
- Thay đổi role
- Reset mật khẩu
- Xem lịch sử đăng nhập (ai đăng nhập lúc nào, đăng xuất lúc nào, từ thiết bị/IP nào)

### Nhật ký hệ thống (Audit Log)
- Xem log hoạt động của **tất cả role** trên hệ thống: ai thao tác gì, vào thời điểm nào, tác động vào dữ liệu nào (tạo/sửa/xóa nhân viên, tạo/sửa/xóa booking, duyệt/từ chối/sửa ý tưởng, phân bổ thù lao theo booking, duyệt/ký hợp đồng booking, thay đổi quyền tài khoản...)
- Lọc log theo: người dùng, loại hành động (tạo/sửa/xóa/duyệt/ký/đăng nhập...), module (Nhân sự/Booking/Ý tưởng/Tài khoản), khoảng thời gian
- Xuất báo cáo log phục vụ kiểm tra/đối soát khi cần
- Cảnh báo các hành vi bất thường (VD: đăng nhập nhiều lần thất bại, xóa hàng loạt dữ liệu)

### Cấu hình hệ thống (kỹ thuật)
- Cấu hình các tham số kỹ thuật của hệ thống (không liên quan chính sách nghiệp vụ, ví dụ: giới hạn dung lượng upload, thời gian hết hạn phiên đăng nhập...)

### Không được phép
- Không truy cập/chỉnh sửa dữ liệu nghiệp vụ (hồ sơ nhân viên, booking, ý tưởng) trừ khi cần hỗ trợ kỹ thuật và được cấp quyền tạm thời

---

## 4. Role Quản lý Hành chính – Nhân sự

Đây là người **chịu trách nhiệm vận hành toàn bộ nhân sự**.

### Quản lý nhân viên
CRUD:
- Thêm nhân viên
- Sửa thông tin
- Xóa/ngừng hoạt động
- Chuyển phòng ban
- Thay đổi chức vụ
- Thay đổi trạng thái nhân viên

Thông tin nhân viên:
- Họ tên
- Avatar
- Ngày sinh
- Số điện thoại
- Email
- Địa chỉ
- Ngày vào công ty
- Phòng ban
- Chức vụ
- Người quản lý
- Loại hợp đồng
- Trạng thái

### Báo cáo
- Báo cáo nhân sự
- Biến động nhân sự
- Hiệu suất nhân viên

---

## 5. Role Nhân viên Hành chính – Nhân sự

Role này chủ yếu **thực hiện nghiệp vụ**, không có quyền quyết định cấp quản lý.

### Được phép
- Xem danh sách nhân viên
- Tạo/sửa hồ sơ nhân viên theo quyền
- Cập nhật giấy tờ
- Theo dõi hợp đồng
- Quản lý hồ sơ
- Xuất báo cáo

### Không được phép
- Thay đổi quyền user
- Xóa dữ liệu quan trọng
- Xem các dữ liệu tài chính nhạy cảm nếu không được phép

---

## 6. Role Quản lý Booking

### Dashboard Booking
Hiển thị:
- Booking đang chờ
- Booking đang thương lượng
- Booking đã chốt
- Booking đang triển khai
- Booking hoàn thành
- Booking hủy
- Doanh thu
- Chi phí
- Lợi nhuận
- Công việc quá hạn
- Hiệu suất từng nhân viên booking

### Quản lý KOL/KOC
Database:
- Tên
- Nền tảng
- Link tài khoản
- Follower
- Engagement
- Chủ đề/niche
- Giá booking
- Địa bàn
- Thông tin liên hệ
- Người phụ trách
- Lịch sử booking
- Đánh giá
- Trạng thái

Ví dụ trạng thái:
Tiềm năng → Đã liên hệ → Đang deal → Đã chốt → Đang chạy → Hoàn thành

### Quản lý Booking
Mỗi booking có:
- Client
- Campaign
- KOL/KOC
- Nội dung
- Deadline
- Ngày đăng
- Giá booking
- Chi phí
- Người phụ trách
- Danh sách nhân viên tham gia (có thể gán nhiều nhân viên cho 1 booking)
- Trạng thái
- File hợp đồng
- File báo giá
- Link bài đăng
- Ghi chú

### Chia lương/thù lao theo booking
QL Booking **vẫn là người quyết định**, vừa chia việc vừa chia thù lao cho từng nhân viên tham gia booking (không cần Giám đốc duyệt riêng cho phần này):
- QL Booking gán nhiều nhân viên vào cùng 1 booking (VD: 3 nhân viên cho 1 booking)
- QL Booking tự nhập/tùy chỉnh số tiền thù lao cho từng nhân viên trên booking đó (VD: booking trị giá 10 triệu → nhân viên A: 3 triệu, B: 3 triệu, C: 4 triệu)
- Hệ thống hiển thị tổng số tiền đã phân bổ so với giá trị booking để QL đối chiếu (cảnh báo nếu tổng phân bổ vượt quá giá trị booking, tùy chính sách công ty có cho phép vượt hay không)
- Có thể chỉnh sửa lại phần phân bổ khi booking thay đổi (thêm/bớt người, đổi số tiền)
- Lưu lịch sử thay đổi phân bổ thù lao (phục vụ đối soát và audit log)
- Giám đốc có thể xem tổng quan phân bổ thù lao của tất cả booking để theo dõi, nhưng không trực tiếp chỉnh sửa
- Nhân viên tham gia booking chỉ xem được phần thù lao của chính mình, không xem được phần của người khác (trừ khi được QL/Giám đốc cấp quyền)

### Thực hiện booking trực tiếp & báo cáo lên Giám đốc
Ngoài vai trò quản lý, QL Booking giờ đây cũng **trực tiếp thực hiện các công việc thực thi booking** như NV Booking, bao gồm:
- Tìm KOL/KOC
- Liên hệ KOL/KOC
- Gửi báo giá
- Thương lượng
- Cập nhật trạng thái booking

Sau khi hoàn tất thông tin booking, QL Booking **gửi đơn phê duyệt booking lên Giám đốc** (xem chi tiết luồng ở mục "Luồng duyệt & ký hợp đồng booking" – phần Giám đốc) trước khi booking được chính thức triển khai.

### Quản lý nhân viên Booking
QL có thể:
- Tạo task
- Giao booking
- Giao deadline
- Theo dõi tiến độ
- Đánh giá nhân viên
- Xem KPI
- Điều phối workload

---

## 7. Role Nhân viên Booking

Nhân viên Booking tập trung vào **thực thi booking**.

### Công việc
- Tìm KOL/KOC
- Thêm KOL vào database
- Liên hệ KOL
- Gửi báo giá
- Thương lượng
- Cập nhật trạng thái
- Theo dõi deadline
- Theo dõi bài đăng
- Upload bằng chứng hoàn thành
- Cập nhật kết quả campaign
- Gửi đơn phê duyệt booking lên Giám đốc khi hoàn tất thông tin booking
- Soạn hợp đồng booking khi được phân công (sau khi booking được Giám đốc duyệt)
- Gửi hợp đồng đã soạn cho Giám đốc để ký
- Xem thù lao được phân bổ cho mình trên từng booking đã tham gia

### Không được
- Xóa booking của người khác
- Thay đổi booking quan trọng
- Duyệt chi phí
- Phân quyền nhân viên
- Xem toàn bộ KPI phòng nếu không được cấp quyền
- Tự thay đổi số tiền thù lao được phân bổ cho mình
- Tự ý ký hợp đồng booking (chỉ Giám đốc mới có quyền ký)

---

## 8. Role Quản lý Ý tưởng

### Idea Management
Nhân viên có thể tạo theo quy trình:

**Ý tưởng → Review → Chỉnh sửa → Duyệt → Triển khai → Hoàn thành**

Mỗi ý tưởng gồm:
- Tên ý tưởng
- Người tạo
- Client
- Campaign
- Insight
- Concept
- Nội dung
- Reference
- Moodboard
- Script
- Deadline
- Người phụ trách
- Người review
- Trạng thái

> **Lưu ý:** Ngoài quy trình Review – Duyệt của QL Ý tưởng, Giám đốc có thêm quyền xem, tự sửa, feedback và **duyệt/từ chối** trực tiếp trên từng ý tưởng — thường áp dụng cho các ý tưởng/campaign quan trọng cần Giám đốc thông qua.

### Quản lý team
QL Ý tưởng có thể:
- Tạo task
- Giao task
- Giao deadline
- Review idea
- Comment
- Yêu cầu sửa
- Duyệt idea
- Theo dõi workload
- Đánh giá nhân viên

### Quản lý kho ý tưởng
Có thể xây dựng một **Idea Library**:
- Idea theo ngành hàng
- Idea theo client
- Idea theo campaign
- Idea theo trend
- Idea viral
- Idea đã triển khai
- Idea chưa sử dụng

---

## 9. Role Nhân viên Ý tưởng

Nhân viên Creative/Idea có thể:
- Tạo ý tưởng
- Chỉnh sửa ý tưởng
- Upload reference
- Viết concept
- Viết script
- Comment
- Nhận task
- Cập nhật tiến độ
- Gửi idea cho manager review
- Xem feedback từ QL Ý tưởng và/hoặc từ Giám đốc
- Chỉnh sửa ý tưởng theo feedback của Giám đốc (nếu có) và gửi lại để Giám đốc duyệt hoặc từ chối

### Trạng thái task
Có thể dùng:
**To do → In Progress → Review → Need Revision → Approved → Done**

Nhân viên **không được tự chuyển sang Approved**.

---

## 10. Yêu cầu bảo mật truy cập mạng

Đây là web app **nội bộ**, chỉ phục vụ nhân viên công ty, do đó áp dụng chính sách giới hạn truy cập theo mạng:

- Hệ thống **chỉ cho phép đăng nhập/truy cập khi thiết bị đang kết nối trong mạng cục bộ (LAN/Wifi nội bộ) của công ty**.
- Khi truy cập từ mạng ngoài (mạng nhà, 4G/5G, wifi quán cà phê...), hệ thống **từ chối đăng nhập**, kể cả khi nhập đúng tài khoản/mật khẩu.
- Kỹ thuật kiểm soát: whitelist theo dải IP nội bộ của công ty (VD: chỉ chấp nhận request có IP nguồn nằm trong dải IP văn phòng), áp dụng ở tầng server/firewall trước khi request chạm tới ứng dụng.
- Áp dụng cho **tất cả role**, không có ngoại lệ ở giai đoạn này (kể cả Giám đốc, AdminIT).
- Nếu nhân viên cố truy cập từ mạng ngoài, hệ thống hiển thị thông báo rõ ràng (VD: "Vui lòng kết nối mạng nội bộ công ty để sử dụng hệ thống") thay vì báo lỗi sai tài khoản/mật khẩu, tránh gây hiểu nhầm.
- Việc cố gắng truy cập từ mạng ngoài (bị chặn) cũng nên được ghi lại trong Audit log (AdminIT xem được) để theo dõi các nỗ lực truy cập bất thường.

> **Lưu ý:** Chức năng cho phép truy cập từ xa có kiểm soát (remote access) khi nhân viên cần làm việc ngoài giờ/ngoài văn phòng **chưa triển khai ở giai đoạn này**, sẽ được mở rộng và thiết kế riêng ở phiên bản sau.

## 11. Tổng hợp các thay đổi

### Cập nhật lần này
- ✅ **Đảo lại** thay đổi về thù lao: chia lương/thù lao theo booking quay lại thuộc toàn quyền **QL Booking** (vừa chia việc, vừa chia thù lao), không cần trình Giám đốc duyệt; Giám đốc chỉ xem tổng quan
- ✅ Nâng quyền Giám đốc trong quản lý ý tưởng: ngoài xem, nay Giám đốc có thể **tự sửa trực tiếp** ý tưởng, hoặc **feedback** yêu cầu chỉnh sửa để nhân viên gửi lại, và có quyền **duyệt hoặc từ chối** ý tưởng
- ✅ Cập nhật ma trận phân quyền: dòng "Chia lương/thù lao theo booking" trả về QL Booking ✅ / Giám đốc 👁️ (xem tổng quan); dòng "Quản lý ý tưởng" nâng Giám đốc lên ✅ (sửa/feedback/duyệt-từ chối nội dung)
- ℹ️ Luồng "Duyệt & ký hợp đồng booking" (đơn phê duyệt booking → Giám đốc duyệt → soạn hợp đồng → Giám đốc ký) giữ nguyên như bản trước, không thay đổi

### Thay đổi ở bản trước
- ✅ Đã bổ sung role **AdminIT** với đầy đủ chức năng: quản lý tài khoản, phân quyền, reset mật khẩu, xem lịch sử đăng nhập
- ✅ Đã bổ sung chức năng **Audit log toàn hệ thống**: ghi nhận mọi thao tác của tất cả role (ai làm gì, khi nào, tác động vào đâu), có bộ lọc và cảnh báo bất thường
- ✅ Đã bổ sung chức năng **Chia lương/thù lao theo booking**: gán nhiều nhân viên vào 1 booking và tự nhập số tiền thù lao riêng cho từng người
- ✅ Đã loại bỏ nội dung liên quan đến **chấm công/nghỉ phép** khỏi phần Giám đốc
- ✅ Đã cập nhật **ma trận phân quyền tổng quan** để bổ sung cột AdminIT và các dòng chức năng mới
- ✅ Giữ nguyên toàn bộ nội dung đúng đã có ở các role: Giám đốc, QL/NV HCNS, QL/NV Booking, QL/NV Ý tưởng
- ✅ Đã bổ sung mục **Yêu cầu bảo mật truy cập mạng**: hệ thống chỉ cho phép đăng nhập trong mạng nội bộ công ty, chặn truy cập từ mạng ngoài; chức năng truy cập từ xa sẽ mở rộng ở giai đoạn sau
