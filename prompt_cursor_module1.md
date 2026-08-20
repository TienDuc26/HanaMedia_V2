# Prompt gửi Cursor — Triển khai Module 1: Đăng nhập & Bảo mật truy cập mạng

Tôi đính kèm file `requirement_HanaMedia_v2.md` (requirement đầy đủ) và `lo_trinh_module_HanaMedia.md` (lộ trình module). **Chỉ làm đúng Module 1** trong lộ trình — không đụng tới các module khác dù có liên quan.

## Phạm vi Module 1 (lấy đúng từ requirement, không thêm bớt)
1. Đăng nhập / đăng xuất
2. Kiểm soát truy cập theo mạng nội bộ (whitelist IP):
   - Chỉ cho đăng nhập/truy cập khi thiết bị đang ở trong dải IP LAN/Wifi nội bộ công ty
   - Từ mạng ngoài (mạng nhà, 4G/5G, wifi quán cà phê...) → từ chối đăng nhập, **kể cả khi đúng tài khoản/mật khẩu**
   - Kiểm tra IP nguồn ở tầng server/middleware, **trước khi** request chạm tới logic xử lý đăng nhập
   - Áp dụng cho **tất cả role**, không ngoại lệ
3. Khi bị chặn do sai mạng: hiển thị thông báo rõ ràng dạng "Vui lòng kết nối mạng nội bộ công ty để sử dụng hệ thống" — **không được** hiển thị chung với lỗi sai tài khoản/mật khẩu (tránh gây hiểu nhầm)

## KHÔNG được làm trong lần này (thuộc module khác, làm sau)
- Không tạo màn hình quản lý tài khoản, tạo/khóa/phân quyền user (thuộc Module 2)
- Không ghi audit log cho các lần bị chặn truy cập ngoài mạng (thuộc Module 3 — nhưng nếu cấu trúc code khiến việc gắn log sau này khó khăn, hãy báo tôi để cân nhắc, đừng tự ý code phần log)
- Không làm remote access / VPN cho phép truy cập ngoài mạng có kiểm soát (requirement ghi rõ: chưa triển khai giai đoạn này)
- Không thêm tính năng "nhớ mật khẩu", "quên mật khẩu", 2FA hay bất kỳ thứ gì không được nêu ở trên, trừ khi tôi yêu cầu thêm

## Quy tắc bắt buộc khi làm việc

### 1. Khảo sát trước khi code
- Đọc toàn bộ cấu trúc project hiện tại (folder structure, package.json/composer.json/requirements.txt..., các file config, model User đã có nếu có)
- Xác định: project đang dùng framework/stack gì, đã có sẵn cơ chế Auth chưa (VD: Passport, NextAuth, Sanctum, JWT tự viết...), đã có model/bảng User/Account chưa và schema hiện tại ra sao
- **Không giả định stack** — báo cáo lại cho tôi những gì bạn tìm thấy trước khi đề xuất cách làm

### 2. Tuân thủ kiến trúc MVC hiện có của project
- Route/Controller/Model phải tách đúng theo cấu trúc MVC (hoặc pattern tương đương project đang dùng, nếu project không theo MVC thuần thì bám theo đúng convention hiện tại, không tự ý đổi kiến trúc)
- Middleware xử lý IP whitelist phải đặt đúng layer middleware (không nhét logic kiểm tra IP vào Controller)
- Không viết logic nghiệp vụ (business logic) trong route file
- Dùng lại đúng cấu trúc thư mục, naming convention, style code đã có sẵn trong project (indent, đặt tên biến, cách import...)

### 3. Không tạo file/folder rác
- Không tạo file test/scratch tạm thời rồi để lại trong project
- Không tự sinh thêm README, comment thừa, file mẫu (boilerplate) không được yêu cầu
- Không để lại code đã comment out, console.log/debug print thừa
- Nếu có tạo file tạm để thử nghiệm, phải xoá trước khi báo cáo hoàn thành
- Sau khi xong, liệt kê chính xác danh sách file đã tạo mới / đã sửa — không được có file nào ngoài danh sách đó

### 4. Tạo folder/file mới → PHẢI xin phép trước, chưa code vội
- Nếu việc triển khai cần tạo **folder mới** hoặc **file mới ở vị trí chưa từng có trong project** (VD: thư mục `middleware/`, `services/auth/`, migration mới...), hãy:
  1. Dừng lại, liệt kê rõ: tên folder/file dự kiến tạo, vị trí, mục đích, vì sao không thể đặt vào cấu trúc hiện có
  2. Trình bày cây thư mục dự kiến (trước/sau) để tôi xem
  3. **Chờ tôi xác nhận** rồi mới tạo và viết code vào đó
- Nếu chỉ cần sửa/thêm vào file, folder đã tồn tại sẵn thì cứ làm bình thường, không cần hỏi

### 5. Khớp với DB hiện có
- Nếu project đã có bảng/model User (hoặc tương đương), dùng lại đúng field hiện có để xử lý đăng nhập — không tạo bảng User thứ hai
- Nếu cần thêm field mới liên quan tới Module 1 (VD: lưu IP đăng nhập gần nhất, nếu cần) mà DB hiện chưa có, liệt kê rõ field/migration đề xuất và hỏi xác nhận trước khi tạo migration

## Việc cần làm theo thứ tự
1. Khảo sát codebase & DB hiện tại → báo cáo lại phát hiện (stack, cấu trúc, Auth đã có gì chưa)
2. Đề xuất cách triển khai Module 1 theo đúng phạm vi ở trên, bám theo MVC hiện có
3. Nếu cần tạo folder/file mới → dừng lại, xin xác nhận theo mục 4
4. Sau khi tôi xác nhận, mới code
5. Sau khi xong: liệt kê danh sách file đã tạo/sửa, xác nhận không còn file rác/debug code, và tóm tắt cách test thử (đăng nhập trong mạng nội bộ vs ngoài mạng)
