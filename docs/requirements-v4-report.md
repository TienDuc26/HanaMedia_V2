# Kết quả triển khai yêu cầu v4

Ngày kiểm tra: 27/09/2026. Nhánh: `codex/requirements-v4`.
Phạm vi đối chiếu: `thay_doi_yeu_cau_v4.md`. Chưa commit, merge hoặc push.

## Đã triển khai

| Yêu cầu | Kết quả / nơi kiểm tra |
|---|---|
| Dashboard & báo cáo | Bỏ thông tin nhân sự mới/nghỉ việc, biến động nhân sự khỏi phần hiển thị được yêu cầu. |
| Công thức Booking | Mặc định 50% công ty, 10% hoa hồng, 40% cát-xê. Chi phí được tính ở server; không nhận chi phí do client gửi. Giám đốc thay đổi tỷ lệ tại Cấu hình nghiệp vụ, tổng phải bằng 100%. |
| Lưu tỷ lệ theo Booking | Booking lưu bản chụp tỷ lệ khi áp dụng công thức. Đổi cấu hình không làm thay đổi tiền của Booking đã lưu. Form sửa cũng tính theo tỷ lệ đã lưu của Booking. |
| Nhiều KOL/KOC | Chọn nhiều KOL khi tạo/sửa Booking; xem đầy đủ tên ở danh sách/chi tiết/phê duyệt/ký hợp đồng. Lịch sử của từng KOL tìm cả quan hệ nhiều KOL. |
| Gán nhân viên / chia thù lao | Không gán nhân viên trong form tạo. Sau khi tạo, QL phụ trách chọn NV Booking và chia tiền tại Chi tiết. Chỉ chia trong quỹ hoa hồng; phần dư thuộc chính QL phụ trách. Không tính trùng tiền QL và NV. |
| Chiến dịch | Theo yêu cầu cập nhật 27/09/2026: QL tạo → Đang chờ ký → Giám đốc chốt → Đang chạy → QL đánh dấu Hoàn thành → QL xác nhận đã nhận tiền, Nghiệm thu → Đã nghiệm thu. Không đổi trạng thái bằng form sửa; Booking chỉ chọn chiến dịch đang chạy và có dấu chốt. Có audit và dấu thời gian. Chi tiết: `docs/campaign-lifecycle.md`. |
| Mở dữ liệu cho Ý tưởng | Phòng Ý tưởng chỉ thấy/chọn chiến dịch đã chốt. Áp dụng kiểm tra tại server cho trang/API, kho ý tưởng, báo cáo, danh sách chọn chiến dịch khi giao việc và tài liệu ý tưởng. |
| Content Creator | Dùng cách A trong tài liệu: đổi nhãn NV Ý tưởng; người phụ trách ý tưởng có thể là nhân viên nội bộ hoặc KOL bên ngoài. Không tạo tài khoản KOL, không biến KOL thành nhân viên. |
| Bản nghiệm thu | Không yêu cầu tải nghiệm thu ở bước tạo. QL tải bản nghiệm thu trong Chi tiết sau khi ký; lưu nghiệm thu chuyển Booking sang Hoàn thành. Báo giá cũ chỉ giữ lại để tra cứu, không đổi nội dung thành nghiệm thu. |
| Pháp lý | Màn hình kiểm tra hợp đồng thật; duyệt hoặc trả sửa kèm lý do; kiểm tra đúng phiên bản. Không thể bỏ qua Pháp lý để ký hoặc duyệt lại phiên bản cũ. |
| Ký hợp đồng | Chỉ Giám đốc ký đúng bản Pháp lý đã duyệt, có tích xác nhận. Sau ký không được thay hợp đồng. QL có thao tác Bắt đầu triển khai và nghiệm thu trong Chi tiết. |
| Kế toán | Tổng hợp theo ngày/tháng/quý; doanh thu/chi phí/lợi nhuận; cát-xê từng KOL, hoa hồng NV và phần QL còn lại; đối soát thanh toán và CSV. Chỉ mở chứng từ hợp đồng đã ký. Không có quyền sửa Booking/thù lao/hợp đồng. Giám đốc xem đối soát nhưng không đánh dấu thanh toán. |
| Hồ sơ nhận lương | Giữ luồng hồ sơ cá nhân đã có; chính chủ sửa. Giám đốc và cả QL/NV HCNS được xem. Bổ sung bảo vệ QR mới và đường dẫn QR cũ; AdminIT, Booking, Kế toán, Pháp lý không xem QR/ngân hàng người khác. Bổ sung chống giả mạo yêu cầu cập nhật hồ sơ. |
| Nhật ký | Chốt chiến dịch, lưu Booking, gửi duyệt, quyết định duyệt, đổi thù lao từng người, gửi hợp đồng, Pháp lý, ký, nghiệm thu, đối soát và đổi cấu hình có nhật ký. |

Giữ sidebar theo role và các tuyến màn hình hiện có. Với Pháp lý/Kế toán, thay liên kết chờ triển khai bằng màn hình nghiệp vụ thật; không thêm phân hệ mới tùy ý vào sidebar các phòng còn lại.

## Luồng vận hành để kiểm tra

### Chiến dịch và Ý tưởng

1. QL Booking tạo chiến dịch; hệ thống đặt Đang chờ ký, không cho chọn trạng thái thủ công.
2. Chiến dịch chờ ký chưa thể được chọn để tạo Booking và chưa hiển thị cho phòng Ý tưởng.
3. Giám đốc vào Chiến dịch → Chốt chiến dịch; hệ thống tự chuyển Đang chạy và mở cho tạo Booking.
4. Phòng Ý tưởng thấy chiến dịch và tạo ý tưởng, chọn Content Creator nội bộ hoặc KOL.
5. Luồng review Ý tưởng và quyết định của Giám đốc vẫn giữ riêng như trước.

### Booking / hợp đồng / kế toán

1. QL Booking tạo Booking nháp, chọn chiến dịch đang chạy, chọn một hoặc nhiều KOL.
2. Server tính tiền theo tỷ lệ cấu hình. Người phụ trách là QL tạo Booking.
3. QL vào Chi tiết → chọn NV tham gia và chia một phần quỹ hoa hồng, hoặc giữ toàn bộ.
4. QL gửi đơn → Giám đốc duyệt hoặc từ chối. Từ chối phải ghi lý do.
5. Đã duyệt → QL hoặc NV được phân công tải hợp đồng → Pháp lý kiểm tra.
6. Pháp lý trả sửa → QL thấy lý do trong Chi tiết, sửa/tải lại → Pháp lý kiểm tra bản mới.
7. Pháp lý duyệt → Giám đốc xem và tích xác nhận ký.
8. QL bắt đầu triển khai; khi hoàn thành tải bản nghiệm thu và link sản phẩm.
9. Kế toán đối soát các khoản chi của Booking đã ký. Đây chỉ là ghi nhận, không thực hiện chuyển tiền.

Ví dụ Booking 100 triệu: công ty 50 triệu; cát-xê 40 triệu; quỹ hoa hồng 10 triệu. Nếu chia NV 3 triệu thì QL còn 7 triệu. Tổng chi vẫn 50 triệu, không phải 53 triệu.

## Quy ước quan trọng

- Cát-xê mặc định chia đều cho các KOL đã chọn; phần lẻ được dồn vào KOL cuối để tổng không sai. Đây là quy ước triển khai vì tài liệu chưa quy định tỷ trọng riêng cho từng KOL. Chưa có màn hình chia cát-xê tùy ý.
- Báo cáo theo kỳ dùng ngày tạo Booking. Doanh thu/chi phí/lợi nhuận là số liệu Booking, không phải báo cáo dòng tiền ngân hàng hay quyết toán thuế.
- Tỷ lệ mới áp dụng cho Booking mới hoặc Booking cũ được chỉnh để áp dụng v4. Không tự tính lại các khoản tiền lịch sử khi chạy migration. Màn hình đối soát đánh dấu rõ dữ liệu cũ và không cho đánh dấu thanh toán dựa vào quỹ chưa xác định.
- Tổng có dữ liệu lịch sử có thể không đạt tỷ lệ 50/10/40; không sửa số lịch sử để ép tổng đẹp.
- Sau khi đã có thanh toán, không đổi phân bổ nhân viên. Kế toán có thể điều chỉnh trạng thái đối soát; mỗi lần đều có nhật ký.
- Cập nhật 27/09/2026: chốt chiến dịch tự chuyển Đang chạy. Migration đưa chiến dịch đang chạy nhưng chưa có dấu chốt về Đang chờ ký; không tự chốt thay Giám đốc, không thay hợp đồng/Booking cũ.
- KOL ngoài công ty không có tài khoản và không trực tiếp nhận task như nhân viên nội bộ.
- Phản hồi Pháp lý hiển thị trong Booking; chưa gửi email/SMS/thông báo đẩy ra ngoài.

## Database và an toàn dữ liệu

- Migration mới: `20260926122214_RequirementsV4Workflow`, kèm designer và snapshot trong Git working tree.
- Đã áp dụng vào database local `HanaMedia` trên `HAIRS-LAPTOP`.
- Bổ sung quan hệ nhiều KOL, phân bổ cát-xê, tỷ lệ tài chính theo Booking, xác nhận chiến dịch, Content Creator bên ngoài, phiên bản/duyệt Pháp lý và thanh toán từng người nhận.
- Số bản ghi trước/sau migration: 13 users, 5 employees, 1 booking, 3 campaigns, 1 idea. Giá trị Booking cũ và chi phí cũ giữ nguyên.
- Không có tài khoản `v4_*` hoặc database thử còn sót lại trong máy sau kiểm thử.
- Backup COPY_ONLY/CHECKSUM đã được RESTORE VERIFYONLY kiểm tra trước migration:
  `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\HanaMedia_V4_Test_ad65689a78aa458cae84a6ef2426c784_source.bak`.
- QR mới và tài liệu Booking mới nằm ngoài thư mục public. Khi bàn giao/backup phải sao lưu cả `App_Data` và `wwwroot/uploads`, không chỉ SQL database. Các file upload không đưa lên Git.
- Không tự rollback migration sau khi đã có dữ liệu mới: Down sẽ bỏ các cột/bảng v4. Nếu cần khôi phục phải đối chiếu backup và file upload trước.

## Kiểm thử thực tế

- Release build thành công: 0 lỗi, còn 6 cảnh báo nullability/async ở code có sẵn.
- 87 kiểm tra tích hợp đạt, trong đó một kiểm tra tổng hợp gọi thêm **30 kiểm tra giao diện/thao tác** ở desktop 1440px và mobile 390px.
- Dùng SQL database sao chép riêng, áp dụng migration rồi tạo fixture. Không tạo Booking/tài khoản/chiến dịch thử trên database chính.
- Đã kiểm tra: nhiều KOL và phần lẻ; từ chối chiến dịch không chạy; khóa chiến dịch chưa chốt; giá trị chi phí giả mạo; vượt quỹ; sửa nhầm Booking người khác; đổi cấu hình/snapshot; duyệt → trả sửa → gửi bản mới → ký; chặn bỏ qua Pháp lý; khóa file đã ký; phân quyền đối soát; tiền thập phân; nghiệm thu; chống giả mạo POST/PUT; quyền ngân hàng/QR; upload PNG thật; xuất CSV; nhật ký.
- Trình duyệt: trang Booking, Chiến dịch, Chi tiết, tổng quan thù lao, ký hợp đồng, cấu hình, Pháp lý, Kế toán, Ý tưởng; giữ sidebar; menu mobile; không tràn toàn trang; không có lỗi JavaScript; xem modal sửa và bảng thù lao.
- Đã xem ảnh kết quả desktop/mobile và chỉnh bảng đối soát mobile thành cuộn ngang nội bộ để nút không bị bó thành từng chữ.
- Bộ test: `tests/V4Flow` và `tests/v4-ui.cjs`. Ảnh kiểm tra lưu local tại `tests/artifacts/v4` (gitignored).
- Chạy lại: `dotnet run --project tests/V4Flow/V4Flow.csproj -c Release -- --ui`. Cần SQL có quyền backup/restore/tạo database test, Node + Playwright trong NODE_PATH và Microsoft Edge. Bỏ `--ui` nếu chỉ kiểm tra HTTP/SQL.
- Thư viện xử lý ảnh nâng từ ImageSharp 3.1.6 lên 3.1.11 để xử lý cảnh báo bảo mật đang có. Đã kiểm tra lại upload QR PNG thật. Tham chiếu bản vá: https://github.com/SixLabors/ImageSharp/security/advisories/GHSA-rxmq-m78w-7wmc

## Bố nên kiểm tra lại

1. Dùng Giám đốc chốt một chiến dịch; kiểm tra phòng Ý tưởng chỉ thấy sau khi chốt.
2. Tạo Booking mới có nhiều KOL; kiểm tra tiền và chọn NV chia hoa hồng trong Chi tiết.
3. Dùng tài khoản Pháp lý trả sửa một lần, duyệt bản mới rồi Giám đốc ký.
4. Dùng Kế toán đối soát từng người nhận; đối chiếu tổng chi, phần QL giữ lại và phần NV.
5. Tự cập nhật ngân hàng/QR bằng tài khoản nhân viên, kiểm tra bằng Giám đốc và HCNS.

Các kiểm tra trên xác nhận phạm vi v4; không thay thế nghiệm thu người dùng, kiểm thử tải lớn hoặc kiểm tra toàn bộ 18 module không thay đổi trong đợt này.
