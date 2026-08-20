# Prompt gửi Cursor — Luồng kiểm tra mạng nội bộ khi đăng nhập (Module 1)

## Luồng chính xác cần implement
1. Chạy chương trình → vào thẳng trang Login bình thường (**không chặn** việc load trang login theo IP)
2. Người dùng nhập tài khoản/mật khẩu, bấm Đăng nhập
3. Khi submit, hệ thống kiểm tra: **IP của thiết bị hiện có nằm trong dải mạng nội bộ được phép hay không**
   - Nếu **đúng dải mạng** → tiếp tục kiểm tra tài khoản/mật khẩu như bình thường (đúng → vào hệ thống, sai → báo lỗi sai tài khoản/mật khẩu như thường lệ)
   - Nếu **sai dải mạng** → **dừng lại ngay, không cần kiểm tra tài khoản/mật khẩu nữa**, hiển thị **popup thông báo riêng biệt**: "Vui lòng kết nối mạng nội bộ công ty để sử dụng hệ thống" — popup này phải khác hẳn với popup báo sai tài khoản/mật khẩu, không được gộp chung hay dùng chung 1 message

> Lưu ý: đây là thay đổi cách triển khai so với bản đầu — kiểm tra mạng nay gắn vào **API xử lý submit login**, không phải chặn toàn bộ site ở tầng middleware/firewall trước khi vào app. Nếu code hiện tại đang chặn ở tầng global middleware (chặn cả trang login), cần điều chỉnh lại đúng theo luồng này: **trang login luôn load được, chỉ chặn ở bước submit**.

## Dải mạng dùng để test tạm thời (CHƯA có mạng công ty thật)
Hiện tại dùng **dải mạng nhà/mạng đang kết nối thật của máy dev** để test tạm, sau này sẽ đổi sang dải mạng công ty:

```
Dải mạng cho phép (tạm thời): 192.168.110.0/24
(Lấy từ: IPv4 192.168.110.213, Subnet Mask 255.255.255.0, Default Gateway 192.168.110.1
— adapter Wi-Fi 2, DNS Suffix "lan", đây là adapter duy nhất đang thực sự kết nối và có gateway)
```

**Bỏ qua hoàn toàn** các adapter ảo/không kết nối khác xuất hiện trong `ipconfig` (VD: `vEthernet WSL` dải `172.24.128.x`, `Ethernet 2` dải `192.168.56.x`, `VMware VMnet1/VMnet8` dải `169.254.x.x` — đây là địa chỉ APIPA/link-local do chưa cấp DHCP, không phải mạng thật, `VPN Client` đang disconnected, `Ethernet` và các Local Area Connection khác đang disconnected). Không dùng bất kỳ dải nào trong số này để whitelist.

> ⚠️ Nếu trong file `.env`/config hiện đang có giá trị `10.33.0.0/16` từ lần cấu hình test trước (dải wifi trường học, đưa nhầm) — **phải đổi lại thành `192.168.110.0/24`**, không giữ lại giá trị cũ.

## Yêu cầu bắt buộc về cách cấu hình dải mạng
- Dải mạng cho phép **phải để trong file cấu hình/biến môi trường** (VD: `.env` → `ALLOWED_NETWORK_CIDR=10.33.0.0/16`), **tuyệt đối không hardcode** giá trị IP trong code logic
- Mục tiêu: sau này khi có mạng công ty thật, tôi chỉ cần **đổi 1 dòng giá trị trong file cấu hình**, không phải sửa code, không phải deploy lại logic
- Nếu cần đổi sang cho phép nhiều dải cùng lúc sau này (VD: nhiều chi nhánh), thiết kế cấu hình dạng cho phép nhận **danh sách nhiều CIDR** (mảng/chuỗi phân tách bằng dấu phẩy), dù hiện tại chỉ cần 1 dải — nhưng **không cần build UI quản lý dải mạng lúc này**, chỉ cần code đọc được danh sách từ config là đủ

## Các điểm kỹ thuật cần xử lý đúng
1. **Lấy đúng IP thật của client** khi submit login:
   - Kiểm tra `req.ip` / `req.socket.remoteAddress` (hoặc tương đương theo framework đang dùng) trả về giá trị gì trong môi trường dev hiện tại — chú ý trường hợp Node/Express có thể trả về dạng IPv4-mapped IPv6 (VD: `::ffff:10.33.97.88`) thay vì `10.33.97.88` thuần, cần chuẩn hoá về IPv4 trước khi so sánh với CIDR
   - Nếu sau này chạy sau reverse proxy/Nginx, cần đọc đúng header `X-Forwarded-For` thay vì IP của proxy — hiện tại nếu project chưa có proxy thì chưa cần xử lý phần này, nhưng hãy viết code theo cách dễ mở rộng, đừng hardcode chỉ đọc `remoteAddress` mà không có chỗ mở rộng
2. **So sánh IP với CIDR** dùng đúng thư viện đang có sẵn trong project (nếu Module 1 trước đó đã có logic so sánh CIDR, tái sử dụng lại, không viết lại từ đầu)
3. Toàn bộ logic kiểm tra IP phải bọc `try/catch`, lỗi bất ngờ không được làm sập server (rút kinh nghiệm từ lỗi `ERR_CONNECTION_REFUSED` trước đó)
4. Ghi log rõ ràng ra console khi dev: IP client nhận được là gì, so với dải cho phép là gì, kết quả pass/block — để tiện debug, không cần lưu vào Audit log DB (Audit log là Module 3, chưa làm ở đây)

## Yêu cầu về UI popup
- Popup "sai mạng" hiển thị **ngay sau khi bấm Đăng nhập**, dùng chung style popup/modal đã có sẵn trong project (không tạo mới component modal nếu đã có sẵn 1 cái dùng chung)
- Nội dung đúng như requirement: *"Vui lòng kết nối mạng nội bộ công ty để sử dụng hệ thống"*
- Không được hiển thị cùng lúc với thông báo sai tài khoản/mật khẩu

## Quy tắc giữ nguyên như các lần trước
- Không tạo file/folder mới nếu chưa cần thiết; nếu bắt buộc phải tạo mới, dừng lại xin xác nhận trước khi code
- Không tạo file rác, không để lại code debug/console.log thừa (trừ log phục vụ debug mạng ở mục kỹ thuật phía trên — cái này giữ lại vì có mục đích rõ ràng)
- Bám đúng cấu trúc MVC/convention hiện có của project

## Sau khi làm xong, báo cáo lại
1. File cấu hình `.env`/config đã thêm (hoặc **sửa lại nếu đã lỡ set sai**) biến `ALLOWED_NETWORK_CIDR` (hoặc tên tương đương) với giá trị `192.168.110.0/24`
2. File nào đã sửa để thêm logic kiểm tra mạng vào bước submit login
3. Kết quả test thử: đăng nhập khi máy đang ở dải `192.168.110.0/24` (phải qua được bước kiểm tra mạng) và cách bạn giả lập trường hợp sai mạng để xác nhận popup hiện đúng
