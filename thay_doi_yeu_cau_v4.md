# Thay đổi yêu cầu (v4) — Đối chiếu với Lộ trình 18 Module

> Tài liệu này **không viết lại toàn bộ yêu cầu**. Đây là bản **bổ sung/điều chỉnh (delta)** dựa trên `requirement_HanaMedia_v2.md` và `lo_trinh_module_HanaMedia.md` đã có. Mỗi thay đổi được gắn rõ: **sửa module nào** hoặc **thêm module mới ở đâu trong lộ trình**.

Quy ước đánh dấu:
- 🔧 **SỬA** = chỉnh sửa nội dung module đã có, không đổi vị trí trong lộ trình
- ➕ **THÊM MODULE MỚI** = chèn module mới vào lộ trình, đặt số dạng `X.1` để không phải đánh số lại toàn bộ 18 module gốc
- ⚠️ **CẦN XÁC NHẬN** = điểm còn mơ hồ, tôi đã chọn 1 phương án hợp lý để tạm đi tiếp, nhưng cần bạn xác nhận lại trước khi code

---

## 1. Role Giám đốc — Dashboard & Báo cáo

### 1.1 🔧 SỬA — Module 16 (Dashboard tổng công ty)
- **Bỏ hẳn** dòng "Nhân sự mới/nghỉ việc" khỏi Dashboard Giám đốc (bỏ cả "nhân sự mới" lẫn "nghỉ việc", không giữ lại phần nào).

### 1.2 🔧 SỬA — Module 17 (Báo cáo)
- **Bỏ** "Biến động nhân sự" khỏi danh sách báo cáo của QL HCNS (mục báo cáo trong requirement gốc, phần 4 — Role QL HCNS).

---

## 2. Công thức tài chính Booking (mới)

### 2.1 ➕ THÊM MODULE MỚI — Module 9.1: "Công thức phân bổ giá trị Booking"
Chèn **ngay sau Module 9 (Booking core)**, **trước Module 10 (Chia thù lao)** — vì Module 10 cần số tiền đã trừ các phần dưới đây mới chia được cho nhân viên.

Nội dung (đã chốt theo làm rõ mới nhất):
- **Biên độ lợi nhuận công ty: 50%** trên giá trị booking
- **Hoa hồng: 10%** trên giá trị booking → thuộc về **QL Booking phụ trách booking đó** (người đứng tên "Người phụ trách" trên booking, không phải chia đều cho mọi QL Booking)
- **Cast (chi phí trả) KOL: 40%** trên giá trị booking
- Tổng 3 phần = 100% giá trị booking → hệ thống tự tính "Chi phí" và "Lợi nhuận" của booking dựa theo công thức này thay vì để QL Booking tự nhập tay như bản cũ
- Các tỷ lệ này nên được lưu dưới dạng **tham số cấu hình** (không hard-code), để Giám đốc chỉnh được sau này → liên kết với **Module 18 (Cấu hình hệ thống nghiệp vụ)**

**Quan hệ với Module 10 (Chia thù lao):** Vì 10% hoa hồng là khoản riêng của QL Booking phụ trách, nên Module 10 ("QL Booking gán nhiều nhân viên vào booking và tự chia thù lao") vận hành **trên chính khoản hoa hồng 10% này** — tức QL Booking phụ trách là người sở hữu quỹ hoa hồng, và có thể tự quyết định giữ toàn bộ hay trích một phần chia cho các nhân viên tham gia (NV Booking) thông qua Module 10. 50% lợi nhuận công ty và 40% cast KOL không nằm trong phạm vi chia thù lao.

### 2.2 🔧 SỬA — Module 16 & 17 (Dashboard/Báo cáo Booking)
- Doanh thu/Chi phí/Lợi nhuận hiển thị trên Dashboard Booking và Báo cáo Booking phải tính theo công thức 50/10/40 ở trên, không còn là số QL Booking tự nhập chi phí tùy ý.

---

## 3. Quản lý Booking — Sửa luồng tạo Booking

Tất cả thay đổi dưới đây đều 🔧 **SỬA Module 9 (Quản lý Booking — lõi)**, không phát sinh module mới.

### 3.1 Chọn nhiều KOL/KOC cho 1 booking
- Field "KOL/KOC" đổi từ chọn 1 → **multi-select** (1 booking có thể gắn nhiều KOL/KOC).

### 3.2 Bỏ hiển thị nhân viên khi tạo booking
- Màn hình **tạo booking** chỉ hiển thị danh sách **KOL/KOC** để chọn, **không hiển thị danh sách nhân viên** ở bước này.
- Việc gán nhân viên tham gia booking **chuyển hẳn sang Module 10** (nơi vốn đã có sẵn thao tác "gán nhiều nhân viên vào booking" khi chia thù lao) — tức là gán nhân viên và chia thù lao giờ làm **cùng một bước, sau khi** booking đã được tạo, thay vì làm ngay lúc tạo booking như bản cũ.

### 3.3 Lọc Campaign theo trạng thái "Đang chạy"
- Dropdown chọn Campaign khi tạo booking **chỉ hiển thị các chiến dịch có trạng thái "Đang chạy"**.
- Yêu cầu bổ sung ngược lại cho **Module 7 (Quản lý chiến dịch)**: cần có field **"Trạng thái chiến dịch"** (hiện requirement gốc chưa liệt kê rõ trạng thái này) — xem thêm mục 4 bên dưới.

### 3.4 Đổi "File báo giá" → "Bản nghiệm thu"
- Field "File báo giá" trong booking đổi thành **"Bản nghiệm thu"**.
- ⚠️ Ý nghĩa nghiệp vụ thay đổi theo: "báo giá" dùng lúc đàm phán (đầu quy trình), "nghiệm thu" dùng lúc xác nhận hoàn thành (cuối quy trình) — tôi hiểu field này chuyển từ mục đích báo giá sang mục đích xác nhận nghiệm thu khi booking hoàn thành. Nếu bạn muốn **giữ cả 2 field** (báo giá lẫn nghiệm thu) thay vì đổi tên 1 field, báo lại.

---

## 4. Quản lý Chiến dịch — Giám đốc chốt chiến dịch (mới)

### 4.1 ➕ THÊM MODULE MỚI — Module 7.1: "Giám đốc chốt chiến dịch"
Chèn **ngay sau Module 7 (Quản lý chiến dịch)**, **trước Module 8, 9, 13**.

Nội dung:
- Giám đốc có thêm hành động **"Chốt chiến dịch"** trên từng campaign (Giám đốc trước đây chỉ 👁️ xem chiến dịch — nay có thêm quyền chốt).
- Chỉ khi chiến dịch được Giám đốc **chốt**, phòng Ý tưởng mới **thấy được** chiến dịch đó để xem/tạo ý tưởng cho chiến dịch này (xem Module 13).
- Mọi lượt chốt chiến dịch ghi vào Audit log (Module 3).

⚠️ **CẦN XÁC NHẬN:** Trạng thái "Đang chạy" (dùng để lọc Campaign khi tạo Booking, mục 3.3) và trạng thái "Đã chốt" (dùng để mở khóa cho phòng Ý tưởng, mục này) — tôi tạm hiểu đây là **2 cờ trạng thái độc lập** trên cùng 1 campaign (1 campaign có thể vừa "Đang chạy" vừa "Đã chốt" cùng lúc). Nếu thực ra bạn muốn đây là **1 chuỗi trạng thái tuần tự duy nhất** (VD: Nháp → Đang chạy → Đã chốt → Kết thúc), báo lại để tôi vẽ lại state machine cho đúng.

### 4.2 🔧 SỬA — Module 13 (Quản lý Ý tưởng — lõi)
- Khi NV/QL Ý tưởng tạo ý tưởng mới, dropdown chọn Campaign **chỉ hiển thị các chiến dịch đã được Giám đốc chốt** (không thấy các campaign chưa chốt).

---

## 5. Gộp KOL/KOC với Nhân viên Ý tưởng thành "Content Creator"

### 5.1 🔧 SỬA — Module 8 (Quản lý KOL/KOC) + Module 13 (Quản lý Ý tưởng)

⚠️ **CẦN XÁC NHẬN — đây là thay đổi mô hình dữ liệu lớn nhất trong đợt này**, tôi cần bạn chốt lại trước khi triển khai vì có 2 cách hiểu rất khác nhau:

- **Cách hiểu A (tôi tạm chọn để đi tiếp):** Chỉ đơn giản là **đổi tên gọi** — role "Nhân viên Ý tưởng" trong hệ thống được gọi là **"Content Creator"**, và khi chọn "người phụ trách" cho một ý tưởng, hệ thống cho phép chọn **cả từ bảng KOL/KOC (Module 8) lẫn từ bảng Nhân viên nội bộ (Module 5)** — tức 1 ý tưởng có thể do nhân viên nội bộ hoặc do 1 KOL/KOC bên ngoài phụ trách, gộp chung dưới nhãn "Content Creator".
- **Cách hiểu B:** Gộp **hẳn 2 role tài khoản** — nghĩa là KOL/KOC được **cấp tài khoản đăng nhập hệ thống** như một nhân viên thật (có thể tự tạo/sửa ý tưởng, nhận task...), không còn là dữ liệu đối tác thuần túy như Module 8 hiện tại.

Cách hiểu A ít rủi ro hơn (không đụng vào Module 2 — tài khoản/phân quyền), cách hiểu B ảnh hưởng tới Module 2, Module 6 (task engine) và toàn bộ luồng bảo mật mạng nội bộ (Module 1) vì KOL là người ngoài công ty. **Vui lòng xác nhận A hay B** (hoặc mô tả rõ hơn) trước khi tôi cập nhật chi tiết Module 8/13.

---

## 6. Hồ sơ cá nhân nhân viên (mới)

### 6.1 ➕ THÊM MODULE MỚI — Module 5.1: "Hồ sơ cá nhân & Tài khoản nhận lương"
Chèn **ngay sau Module 5 (Quản lý nhân sự)**, phụ thuộc **Module 2** (tài khoản) + **Module 5** (hồ sơ nhân viên).

Nội dung:
- Khi AdminIT tạo tài khoản cho nhân sự (Module 2), hệ thống tự sinh kèm 1 **trang thông tin cá nhân** gắn với tài khoản đó.
- Trang này gồm: thông tin cá nhân (có thể tái sử dụng các field đã có ở Module 5: họ tên, ngày sinh, SĐT, email, địa chỉ, avatar...) + **thông tin tài khoản nhận lương** (số tài khoản ngân hàng, tên ngân hàng, chủ tài khoản...).
- **Tài khoản nhận lương do chính nhân viên sở hữu tự cập nhật** (nhân viên tự sửa phần này của mình, không phải HCNS nhập hộ).
- **Quyền xem:** chỉ **Giám đốc** và **phòng HCNS** xem được thông tin tài khoản nhận lương của nhân viên; bản thân nhân viên chỉ xem/sửa được của chính mình; các role khác không xem được.

⚠️ **CẦN XÁC NHẬN:** "phòng HCNS" ở đây gồm cả **QL HCNS lẫn NV HCNS**, hay chỉ **QL HCNS**? Tôi tạm để **cả QL HCNS và NV HCNS** đều xem được (vì bạn ghi chung là "phòng HCNS"), khác với dữ liệu lương/phụ cấp khác mà bản gốc từng giới hạn NV HCNS không được xem nếu không được phép. Nếu muốn siết lại chỉ QL HCNS xem được, báo lại.

---

## 9. Thêm Role Pháp lý — Sửa luồng duyệt & ký hợp đồng Booking

### 9.1 ➕ ROLE MỚI: "Nhân viên Pháp lý" (NV Pháp lý)
- Chức năng chính: **kiểm tra tính hợp lệ của hợp đồng booking** trước khi trình Giám đốc ký.
- ✅ **ĐÃ CHỐT:** Pháp lý chỉ có **1 cấp role duy nhất** (không có QL Pháp lý). Mọi NV Pháp lý đều có quyền duyệt/từ chối hợp đồng như nhau; hệ thống không phân cấp duyệt lại nội bộ giữa các NV Pháp lý.

### 9.2 🔧 SỬA TOÀN BỘ — Module 11 (Luồng duyệt & ký hợp đồng booking)
Thay thế hoàn toàn luồng cũ (đơn giản: gửi duyệt → Giám đốc duyệt → soạn hợp đồng → Giám đốc ký) bằng luồng mới có thêm bước kiểm tra pháp lý:

1. Booking đã hoàn tất thông tin (Client, KOL/KOC, nội dung, giá booking...) → QL Booking phụ trách **gửi đơn phê duyệt booking lên Giám đốc**.
2. **Giám đốc duyệt/từ chối** đơn phê duyệt booking.
3. Nếu Giám đốc **duyệt** → **QL Booking tự soạn hợp đồng** (được quyền tự làm và tự sửa hợp đồng), sau đó **gửi hợp đồng cho NV Pháp lý kiểm tra**.
4. **NV Pháp lý xem xét hợp đồng:**
   - Nếu **duyệt** (hợp lệ) → hợp đồng được chuyển tiếp cho **Giám đốc ký**.
   - Nếu **không duyệt** (có vấn đề) → hệ thống **báo lại cho QL Booking phụ trách booking đó**, kèm ghi chú lý do; QL Booking **sửa lại hợp đồng và gửi lại** cho NV Pháp lý kiểm tra (lặp lại bước 3–4 cho đến khi được duyệt).
5. Sau khi NV Pháp lý duyệt, **Giám đốc xem lại và tự tích ký (xác nhận ký)** hợp đồng trên hệ thống.
6. Hợp đồng sau khi Giám đốc ký được coi là **chính thức có hiệu lực**.
7. Toàn bộ các bước (gửi đơn, duyệt/từ chối booking, gửi hợp đồng, Pháp lý duyệt/từ chối, yêu cầu sửa, gửi lại, ký) đều **ghi vào Audit log** (Module 3).

**Trạng thái hợp đồng/booking cập nhật (thay thế chuỗi trạng thái cũ):**
```
Chờ duyệt booking
  → Đã duyệt (chờ soạn hợp đồng)
  → Chờ Pháp lý kiểm tra
  → [Pháp lý từ chối] → Yêu cầu sửa (quay lại QL Booking) → Chờ Pháp lý kiểm tra (lặp lại)
  → [Pháp lý duyệt] → Chờ Giám đốc ký
  → Đã ký (hiệu lực)
```

### 9.3 🔧 SỬA — Ma trận phân quyền tổng quan
Thêm cột **Pháp lý**, cập nhật dòng "Duyệt & ký hợp đồng booking":

| Chức năng | Giám đốc | QL Booking | NV Booking | Pháp lý |
|---|---|---|---|---|
| Duyệt & ký hợp đồng booking | ✅ (duyệt đơn + ký) | 👁️ (gửi đề xuất, soạn/sửa hợp đồng) | 👁️ (soạn hợp đồng nếu được phân công) | ✅ (duyệt/từ chối nội dung hợp đồng) |

### 9.4 🔧 SỬA — Module 2 (Tài khoản & Phân quyền)
- Bổ sung role **"Nhân viên Pháp lý"** vào danh sách role mà AdminIT có thể tạo/gán tài khoản.

---

## 10. Thêm Role Kế toán — Đối soát tài chính

Bạn yêu cầu tôi **thiết kế** phạm vi Kế toán ("kiểm kê những gì bên trong hệ thống") — đây là đề xuất dựa trên dữ liệu tài chính đã có sẵn trong hệ thống (Module 9.1, Module 10, Module 11). **Đây là bản nháp, cần bạn duyệt lại** trước khi đưa vào code.

### 10.1 ➕ ROLE MỚI: "Nhân viên Kế toán" (NV Kế toán)
✅ **ĐÃ CHỐT:** Kế toán chỉ có **1 cấp role duy nhất** (không có QL Kế toán). Mọi NV Kế toán có quyền xem/đối soát/đánh dấu thanh toán như nhau trong phạm vi mô tả ở Module 19.

### 10.2 ➕ THÊM MODULE MỚI — Module 19: "Kế toán — Đối soát tài chính"
Chèn **cuối Giai đoạn 4**, sau Module 18 — vì kế toán cần dữ liệu từ hầu hết các module nghiệp vụ đã hoàn thiện (Booking, thù lao, hợp đồng).

Đề xuất phạm vi (dựa trên dữ liệu đã có sẵn trong hệ thống, "kiểm kê" = xem + đối soát + đánh dấu trạng thái thanh toán, **không sửa dữ liệu nghiệp vụ gốc**):

- **Xem tổng hợp Doanh thu – Chi phí – Lợi nhuận** theo từng booking, theo kỳ (ngày/tháng/quý) — lấy theo công thức 50/10/40 ở Module 9.1
- **Đối soát khoản Cast KOL (40%)**: xem danh sách KOL/KOC cần chi trả theo từng booking, đánh dấu **Đã thanh toán / Chưa thanh toán**
- **Đối soát khoản hoa hồng (10%)**: xem hoa hồng của từng QL Booking phụ trách theo từng booking, và phần đã chia lại cho nhân viên (nếu có, theo Module 10), đánh dấu **Đã thanh toán / Chưa thanh toán**
- **Xem danh sách hợp đồng đã ký** (Module 11) để đối chiếu chứng từ, chỉ xem hợp đồng ở trạng thái "Đã ký (hiệu lực)"
- **Xuất báo cáo tài chính/kế toán** theo kỳ (tổng doanh thu, tổng cast KOL, tổng hoa hồng, tổng lợi nhuận)
- **Không có quyền**: sửa booking, sửa thù lao, sửa hợp đồng, phân quyền tài khoản — Kế toán chỉ xem, đối soát và đánh dấu trạng thái thanh toán
- Mọi thao tác đánh dấu thanh toán được ghi vào Audit log (Module 3)

### 10.3 🔧 SỬA — Ma trận phân quyền tổng quan
Thêm cột **Kế toán** + dòng mới:

| Chức năng | Giám đốc | Kế toán | Các role khác |
|---|---|---|---|
| Đối soát tài chính (doanh thu/chi phí/lợi nhuận/thanh toán) | 👁️ (xem tổng quan) | ✅ | ❌ |

### 10.4 🔧 SỬA — Module 2 (Tài khoản & Phân quyền)
- Bổ sung role **"Nhân viên Kế toán"** vào danh sách role mà AdminIT có thể tạo/gán tài khoản.

---

## 11. Cập nhật thứ tự lộ trình (chỉ phần bị ảnh hưởng)

```
Giai đoạn 0 (Nền tảng):     1 → 2 → 3                         (không đổi)
Giai đoạn 1 (Dữ liệu nền):  4 → 5 → 5.1 (MỚI) → 6
Giai đoạn 2 (Booking):      7 → 7.1 (MỚI) → 8 → 9 → 9.1 (MỚI) → 10 → 11 (SỬA — thêm bước Pháp lý) → 12
Giai đoạn 3 (Ý tưởng):      13 → 14 → 15
Giai đoạn 4 (Tổng hợp):     16 → 17 → 18 → 19 (MỚI — Kế toán)
```

Module 6, 12, 14, 15, 18 không có thay đổi trực tiếp trong đợt này (18 chỉ nhận thêm tham số cấu hình % từ mục 2.1).

---

## 12. Tổng hợp các câu cần bạn xác nhận trước khi triển khai

1. "File báo giá" đổi hẳn thành "Bản nghiệm thu", hay giữ cả 2 field song song? *(mục 3.4)*
2. "Đang chạy" và "Đã chốt" của Campaign là 2 cờ độc lập, hay 1 chuỗi trạng thái tuần tự? *(mục 4.1)*
3. **Quan trọng nhất:** Gộp KOL với Nhân viên Ý tưởng thành Content Creator — chọn Cách hiểu A (chỉ gộp nhãn/tham chiếu dữ liệu) hay Cách hiểu B (KOL có tài khoản đăng nhập thật)? *(mục 5.1)*
4. "Phòng HCNS" được xem tài khoản nhận lương — gồm cả NV HCNS hay chỉ QL HCNS? *(mục 6.1)*
5. Phạm vi chức năng Kế toán ở mục 10.2 vẫn là bản **đề xuất/nháp** do tôi thiết kế dựa trên dữ liệu sẵn có (Module 9.1, 10, 11) — cần bạn duyệt lại hoặc chỉnh sửa trước khi đưa vào code, dù cấp role đã chốt là 1 cấp.

> Đã chốt xong, không cần xác nhận thêm:
> - Mục 1.1 (bỏ nhân sự mới/nghỉ việc khỏi Dashboard)
> - Mục 2.1 (hoa hồng 10% thuộc QL Booking phụ trách booking đó, công thức 50/10/40 trên giá trị booking)
> - Mục 9.1 (Pháp lý chỉ 1 cấp role "NV Pháp lý", không có QL Pháp lý)
> - Mục 9.2 (luồng duyệt & ký hợp đồng: QL Booking gửi Giám đốc duyệt → QL Booking soạn/sửa hợp đồng → NV Pháp lý check → duyệt thì chuyển Giám đốc ký, không duyệt thì trả về QL Booking phụ trách sửa và gửi lại)
> - Mục 10.1 (Kế toán chỉ 1 cấp role "NV Kế toán", không có QL Kế toán)
