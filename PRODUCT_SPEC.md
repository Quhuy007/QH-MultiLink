# QH Multi-Link Shortener

Ứng dụng desktop chỉ dành cho Windows 10/11 để rút gọn một URL qua nhiều dịch vụ cùng lúc, có thể đặt mật khẩu khi nhà cung cấp hỗ trợ.

## Stack đã thống nhất

- C# và .NET 10
- WinUI 3 cho giao diện Windows hiện đại
- MVVM (CommunityToolkit.Mvvm) để tách giao diện và trạng thái
- Generic Host cùng `IHttpClientFactory` để quản lý vòng đời ứng dụng và tái sử dụng kết nối
- SQLite cho lịch sử rút gọn và tùy chọn cục bộ
- `Microsoft.Extensions.Http.Resilience` cho timeout, retry có giới hạn và circuit breaker

## Luồng chính

1. Nhập URL dài; kiểm tra scheme `http`/`https`.
2. Hỏi người dùng có cần bảo vệ link bằng mật khẩu không.
3. Nếu có, chỉ liệt kê nhà cung cấp có hỗ trợ mật khẩu; nếu không, liệt kê mọi nhà cung cấp đã bật.
4. Cho phép chọn nhiều dịch vụ và bắt đầu tác vụ đồng thời với giới hạn concurrency.
5. Cập nhật từng kết quả ngay khi provider đó hoàn tất; lỗi của một provider không chặn các kết quả khác.
6. Cho phép sao chép từng link, mở link, tạo QR cục bộ và xem lịch sử.

## Kiến trúc gợi ý

- `QH.MultiLinkShortener.App`: WinUI 3, điều hướng, composition root và giao diện.
- `QH.MultiLinkShortener.Core`: hợp đồng provider, mô hình, validation và điều phối.
- `QH.MultiLinkShortener.Infrastructure`: `IHttpClientFactory`, adapter từng dịch vụ, SQLite, bảo mật token.
- `IShortenerProvider`: mỗi nền tảng là adapter độc lập, khai báo tên, khả năng mật khẩu, cấu hình và phương thức rút gọn.
- `ShorteningCoordinator`: chạy song song có giới hạn, phát kết quả theo từng provider qua luồng/callback bất đồng bộ; hỗ trợ hủy bằng `CancellationToken`.

## Tốc độ, RAM và độ tin cậy

- Dùng client được quản lý bởi factory cho mỗi cấu hình provider; không tạo `HttpClient` mới mỗi lần bấm.
- Không gửi request giả để “warm up” mọi dịch vụ. Các request thật của provider được chọn chạy đồng thời.
- Timeout theo provider, giới hạn concurrency ban đầu 4–6, hỗ trợ hủy.
- Retry có giới hạn và chỉ cho lỗi tạm thời/an toàn; không retry mù POST có thể tạo link trùng.
- Circuit breaker theo provider để lỗi lặp lại không làm hàng đợi chậm.
- Giới hạn response, parse JSON tối thiểu và tránh giữ bản sao dữ liệu không cần thiết.
- Ước tính sơ bộ khi nhàn rỗi: khoảng 50–120 MB tùy WinUI, driver và cấu hình; cần đo trên bản build thực tế.

## Bảo mật

- Không ghi token/API key vào source, log, SQLite dạng rõ hoặc thông báo lỗi; lưu bằng Windows Credential Locker hoặc DPAPI.
- Che URL nhạy cảm và mật khẩu khỏi log; không lưu mật khẩu vào lịch sử.
- Chỉ gửi dữ liệu đến provider người dùng đã chọn; dùng HTTPS và xác thực chứng thư mặc định.
- Không tự tải URL đích.
- Chỉ gửi mật khẩu đến provider đã công bố hỗ trợ; báo rõ kết quả bảo vệ theo phản hồi provider.

## Dữ liệu và lịch sử

Lưu provider, URL rút gọn, thời điểm, trạng thái và metadata không bí mật. Tùy chọn lưu URL gốc nên mặc định tắt; hỗ trợ xóa lịch sử. QR được tạo cục bộ từ URL rút gọn.

## Trạng thái

Đã triển khai bằng WinUI 3/.NET 10 với spoo.me v1, da.gd, clck.ru dùng ngay; TinyURL, Dub, Short.io cần key riêng. Tạm gỡ is.gd và v.gd khỏi catalog hoạt động. Không nhúng API key. Xem README.md để chạy ứng dụng; PROVIDER_RESEARCH.md ghi nguồn chính thức, đo tốc độ và giới hạn kiểm tra.

## Cập nhật giao diện và ưu tiên dịch vụ

- Luồng mặc định dùng một dịch vụ ưu tiên đã lưu, không hỏi lại mỗi lần; spoo.me là mặc định ban đầu. Mật khẩu tự chọn provider tương thích đã cấu hình; không tự failover khi provider lỗi.
- Nhiều dịch vụ và mật khẩu nằm trong Tùy chọn mở rộng. Đã bỏ chế độ thu gọn trên giao diện chính; cửa sổ nổi độc lập cung cấp luồng tạo nhanh.
- Giao diện chính theo mẫu Framer mới: sidebar dọc, khối chọn chung có chuyển động, tiêu đề tối giản, thẻ nội dung bo góc; chú thích qua tooltip có hỗ trợ bàn phím.
- Màu nền RGB và GIF/MP4 cục bộ: tối đa 15 giây/một vòng, 20 MiB, Full HD; GIF tối đa 450 frame. Từ chối tệp quá dài; không cắt clip. Tắt âm, phát lặp, có dừng/phát, tôn trọng reduced motion/high contrast.
- Nền được sao chép riêng, không sửa bản gốc, không gửi mạng. Cài đặt mới tương thích dữ liệu SQLite cũ.

## Cửa sổ phụ theo bản phác thảo (21/10/2026)

Tab mép phải → nhập URL và mật khẩu tùy chọn → loading nhỏ → kết quả hai dòng (link, Sao chép/Ẩn) → tab. Kích thước mở 281 × 87 ở DPI 100%, bám vùng làm việc màn hình chính và điều chỉnh theo DPI/taskbar. Một Window riêng, không đổi nội dung hoặc kích thước giao diện chính. Dùng chung provider/cài đặt/lịch sử nhưng trạng thái nhập, kết quả và hủy độc lập. Đóng giao diện chính chỉ ẩn nó; menu tab cho phép mở lại hoặc thoát toàn bộ.

## Giao diện chính theo mẫu Framer

Tham chiếu https://qhshortlink.framer.website/ cho bố cục/sidebar/sáng tối. Sidebar có khối chọn chung trượt giữa Tạo liên kết, Lịch sử và Cài đặt, kết hợp chuyển nội dung nhẹ; tắt chuyển động theo thiết lập Windows. Công tắc sáng/tối ở chân sidebar tự lưu riêng. Giữ đầy đủ các chức năng trong từng mục, dữ liệu/key/nền và cửa sổ phụ. Khung tạo/kết quả hai cột khi đủ rộng, một cột khi hẹp; sidebar chỉ biểu tượng khi cửa sổ nhỏ.
