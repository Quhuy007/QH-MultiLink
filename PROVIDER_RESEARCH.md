# Dịch vụ rút gọn link — kiểm tra ngày 18/10/2026

Đã cập nhật ứng dụng với 6 dịch vụ: 3 dùng ngay, 3 cần key riêng. Tạm gỡ is.gd và v.gd khỏi danh sách hoạt động; adapter cũ chỉ được giữ để tham chiếu và kiểm thử. Lịch sử cũ không bị xóa. Nếu dịch vụ mặc định cũ là is.gd/v.gd, ứng dụng đổi sang spoo.me và báo trước khi tạo link tiếp theo.

## Kết quả thực tế từ máy này

URL thử: `https://example.com/`, không mật khẩu, không key. Không mở URL đích. Mỗi cột là một lần đo, gồm mạng và thời gian xử lý, không phải SLA hoặc benchmark thống kê. Dịch vụ có thể trả lại link đã có cho cùng URL.

| Dịch vụ | Khảo sát HTTP | Kiểm tra adapter tích hợp | Trạng thái |
|---|---:|---:|---|
| spoo.me | 542 ms | 1.664 ms | Tạo thành công |
| da.gd | 800 ms | 999 ms | Tạo thành công |
| clck.ru | 1.132 ms | 1.160 ms | Tạo thành công |
| is.gd / v.gd | Không nhận được kết quả hợp lệ | Không bật lại | Tạm gỡ theo phản ánh người dùng và phép thử cục bộ; không kết luận ngừng hoạt động toàn cầu |
| CleanURI | Timeout sau khoảng 12 giây | Không tích hợp | Chưa đáp ứng thử nghiệm mạng hiện tại |
| Ulvis | POST 404; GET trả `success:false`, `domain not allowed` | Không tích hợp | Chưa có kết quả thành công với URL mẫu |

Kết quả khảo sát thô: `artifacts/research/providers.json`. Mã lỗi exception của is.gd/v.gd không đủ để kết luận nguyên nhân chính xác. Dữ liệu chỉ chứa URL mẫu công khai.

Lần kiểm tra đầu ra JSON sau đó cũng thành công 3/3: spoo.me 860 ms, da.gd 876 ms, clck.ru 1.061 ms. Báo cáo mới nhất: `artifacts/smoke/results.json`. Sự chênh lệch giữa các lần cho thấy chưa thể xếp hạng tốc độ cố định từ vài mẫu.

## Các kết nối bằng key

| Dịch vụ | Cần nhập | Phương thức | Mật khẩu trong app |
|---|---|---|---|
| TinyURL | API token có quyền tạo link | POST `https://api.tinyurl.com/create` | Không |
| Dub | API key của workspace | POST `https://api.dub.co/links` | Có, trạng thái đã gửi yêu cầu |
| Short.io | Public API key và domain thuộc tài khoản | POST `https://api.short.io/links/public` | Có; xác nhận theo `hasPassword` nếu API trả trường này |

Đã kiểm thử hợp đồng request/response giả lập cho cả ba; **chưa thử trực tiếp bằng tài khoản thật** vì chưa có key người dùng. Khả năng tạo link, mật khẩu, quota và tính phí phụ thuộc gói/quyền tài khoản hiện hành. Không dùng chung key nhúng sẵn.

Vào **Cài đặt → Kết nối dịch vụ**, chọn dịch vụ, dán key rồi bấm **Lưu key dịch vụ này**. Có thể chuyển giữa các dịch vụ để nhập nhiều key; phần nhập dở được giữ riêng trong bộ nhớ cho tới khi lưu hoặc đóng app. Nút **Lưu cài đặt** lưu tất cả key đang chờ cùng cài đặt chung. Với Short.io nhập cả domain, không có `https://` hay đường dẫn; có thể đổi domain mà giữ key đã lưu. Key đã lưu không điền lại vào ô nhập: xem trạng thái **Đã lưu key mã hóa**. Để trống key giữ nguyên kết nối. Key và cấu hình Short.io được mã hóa DPAPI theo tài khoản Windows. Xóa key chỉ tác động dịch vụ đang chọn. Lưu key trên máy chưa xác nhận key có quyền/hạn mức tại nhà cung cấp.

## Gọi HTTP trực tiếp giống requests/httpx

Ứng dụng C# đã dùng `HttpClient` + `IHttpClientFactory`: gửi GET/POST, JSON/form, header Authorization, giới hạn thời gian, hủy yêu cầu và tái sử dụng kết nối. Không cần thêm Python để làm việc này.

da.gd và clck.ru cung cấp endpoint HTTP công khai được tài liệu hóa, trả về **văn bản**. Adapter đọc và kiểm tra URL HTTPS, rồi chuẩn hóa thành `ProviderLink`; các API khác trả JSON được parse theo từng schema. Không thể buộc máy chủ trả JSON chỉ bằng cách đặt header `Accept: application/json`.

Với trang không có API công bố, có thể viết adapter cho request mà trang thực sự sử dụng, gồm form/JSON, cookie và CSRF khi được phép. Nếu phản hồi là HTML thì cần bộ phân tích HTML riêng. Endpoint nội bộ có thể đổi hoặc yêu cầu phiên đăng nhập/CAPTCHA; vì vậy chưa đưa endpoint như thế vào bản hiện tại khi đã có các lựa chọn được tài liệu hóa và thử thành công.

Không tự retry thao tác tạo link, kể cả GET; không tự đổi nhà cung cấp sau lỗi. Giới hạn phản hồi 64 KiB, không tự theo redirect API, tôn trọng 429/Retry-After và circuit breaker. clck.ru giới hạn URL 2.000 ký tự theo điều khoản. Link gốc chỉ được gửi tới dịch vụ đã chọn.

## Chạy lại kiểm tra và nhận JSON

```powershell
dotnet test tests/QH.MultiLinkShortener.Tests
dotnet run --project tools/QH.SmokeCheck -- --live --json
```

Lệnh live tạo link thật cho URL mẫu, chỉ dùng các dịch vụ không cần key và không đọc key/lịch sử người dùng. Kết quả chuẩn hóa lưu tại `artifacts/smoke/results.json`, gồm nhà cung cấp, link, trạng thái, lỗi đã làm sạch và số ms. Lệnh `--research` là công cụ khảo sát độc lập và có thể gọi cả những dịch vụ chưa được bật trong app.

## Nguồn chính thức

- [da.gd: HTTP shortening](https://da.gd/help).
- [clck.ru: API trên trang chủ](https://clck.ru/), [điều khoản về tạo link tự động và giới hạn URL](https://yandex.ru/legal/clck_termsofuse/ru/).
- [TinyURL: sử dụng API và Bearer token](https://tinyurl.com/blog/how-use-try-it-out-tinyurl/), [API reference](https://tinyurl.com/app/dev).
- [Dub: tạo link](https://dub.co/docs/api-reference/links/create).
- [Short.io: API public dành cho client](https://developers.short.io/reference/post_links-public).
- [spoo.me: tạo link](https://spoo.me/docs/api-reference/url-shortening/create-shortened-url).
- [CleanURI](https://cleanuri.com/docs), [Ulvis](https://ulvis.net/developer.html).
