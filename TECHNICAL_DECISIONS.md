# QH Multi-Link Shortener — quyết định kỹ thuật

## Mục tiêu phiên bản đầu

Ứng dụng Windows 10/11 cho phép nhập một URL, chọn nhiều provider, chạy đồng thời có giới hạn, xem kết quả từng dịch vụ ngay khi hoàn tất, sao chép link, tạo QR cục bộ và lưu lịch sử SQLite.

## Quyết định

- UI: WinUI 3, C#/.NET 10, MVVM.
- Vòng đời ứng dụng: Generic Host và dependency injection.
- HTTP: `IHttpClientFactory`, named/typed clients theo provider.
- Độ tin cậy: timeout, concurrency limit, circuit breaker riêng theo provider; retry chỉ khi thao tác được xác định an toàn.
- Lưu trữ: SQLite; token trong Windows Credential Locker/DPAPI, không lưu trong DB.
- Provider: adapter độc lập, metadata về hỗ trợ mật khẩu; trong chế độ cần mật khẩu thì ẩn provider không hỗ trợ.

## Điều phối

Khi bấm tạo link, khởi chạy các provider đã chọn cùng lúc. Mỗi provider có trạng thái `Pending → Running → Success/Failed/Cancelled`; UI nhận kết quả qua dispatcher mà không đợi cả nhóm. Dùng semaphore tránh cạn tài nguyên và cho phép hủy. Lỗi của một provider không hủy các provider khác.

## Cần chốt trước khi viết adapter

1. Danh sách dịch vụ ưu tiên và API chính thức đang hoạt động.
2. Provider nào yêu cầu API key và cách nhập/kiểm tra token.
3. Có lưu URL gốc trong lịch sử không (đề xuất mặc định không lưu).
4. Concurrency mặc định và timeout sau khi đo trên mạng thực.

Không nhúng thông tin đăng nhập hoặc giả định API undocumented. Mỗi adapter cần kiểm tra điều khoản, response lỗi, rate limit và hỗ trợ mật khẩu theo tài liệu chính thức.

## Quyết định triển khai bản đầu (18/10/2026)

- Provider hiện hành: spoo.me v1, da.gd, clck.ru (dùng ngay); TinyURL, Dub và Short.io (key riêng). is.gd/v.gd tạm gỡ khỏi catalog, giữ lịch sử và adapter tham chiếu.
- URL gốc mặc định không lưu; có tùy chọn bật trong Cài đặt. Không lưu mật khẩu link.
- Concurrency mặc định 4, timeout 20 giây, có thể điều chỉnh. Chưa tối ưu theo benchmark mạng thực.
- da.gd và clck.ru dùng endpoint GET được công bố; các dịch vụ còn lại dùng POST. Không retry tự động thao tác tạo link trên cả hai phương thức. Circuit breaker theo named client; cooldown khi nhận rate limit.
- API key dùng DPAPI CurrentUser; SQLite chỉ lưu metadata/cài đặt không bí mật.
- spoo.me không trả trường xác nhận bảo vệ riêng: hiển thị Requested (đã gửi theo API), không gán Confirmed.
- Giao diện tiếng Việt, theme sáng và Windows high contrast; thiết kế theo ui-ux-pro-max, native Segoe UI và Fluent icons.
- Xuất bản unpackaged, self-contained x64; giữ toàn bộ thư mục khi phân phối.

## Cập nhật giao diện và ưu tiên dịch vụ

- Luồng mặc định dùng một dịch vụ ưu tiên đã lưu, không hỏi lại mỗi lần; spoo.me là mặc định ban đầu. Ưu tiên cũ is.gd/v.gd được chuyển sang spoo.me kèm thông báo. Mật khẩu tự chọn provider tương thích đã cấu hình; không tự failover khi provider lỗi.
- Nhiều dịch vụ và mật khẩu nằm trong Tùy chọn mở rộng; cửa sổ nổi độc lập thay thế chế độ thu gọn của giao diện chính.
- Giao diện chính theo mẫu Framer mới dùng sidebar, tiêu đề tối giản và thẻ bo góc; tooltip có hỗ trợ bàn phím.
- Màu nền RGB và GIF/MP4 cục bộ: tối đa 15 giây/một vòng, 20 MiB, Full HD; GIF tối đa 450 frame. Từ chối tệp quá dài; không cắt clip. Tắt âm, phát lặp, có dừng/phát, tôn trọng reduced motion/high contrast.
- Nền được sao chép riêng, không sửa bản gốc, không gửi mạng. Cài đặt mới tương thích dữ liệu SQLite cũ.

## Mở rộng dịch vụ và HTTP

`IShortenerProvider.IsConfigured` phân biệt có cấu hình với tình trạng hoạt động thực tế. Không gọi mạng khi thiếu key/domain. `ProviderInfo` khai báo tên secret, key bắt buộc và domain bắt buộc; UI dùng cùng catalog thay vì danh sách cố định. Key tách theo dịch vụ, lưu DPAPI; Short.io public key và domain được mã hóa cùng nhau. Không tự kiểm tra key bằng cách tạo link ngoài ý muốn.

`HttpClient` cung cấp chức năng tương đương requests/httpx. Adapter chuẩn hóa JSON hoặc plain text thành `ProviderLink`; không giả định mọi endpoint trả JSON. Chưa tích hợp endpoint nội bộ không có tài liệu. Xem [PROVIDER_RESEARCH.md](PROVIDER_RESEARCH.md) cho nguồn, phép thử và giới hạn.

## Cửa sổ nổi độc lập

`QuickWindow` là WinUI Window thứ hai, OverlappedPresenter không border/title bar, always-on-top, IsShownInSwitchers=false. Không đặt owner vào giao diện chính. `QuickShorteningSession` quản lý Collapsed/Input/Loading/Result/Error riêng, gọi coordinator hiện có và chỉ một provider mỗi tác vụ. Hai cửa sổ dùng chung instance provider và storage, giữ cooldown/rate limit nhất quán.

Khung mở 281 × 87 logical px; tab/loading 44 × 52. `EdgePlacement` neo vùng làm việc màn hình chính, clamp Y, cập nhật DPI/vùng làm việc khi chạy. Dùng nút native cao 32 trong khung 87 px theo bản phác thảo; phím Enter/Tab/Escape, tên trợ năng và tooltip bổ sung khả năng thao tác trong diện tích nhỏ. Đóng main chỉ Hide; menu tab có Open main và Exit. Exit chờ hai tác vụ hoàn tất hủy trước khi giải phóng host/window.

Nguồn Windows: [nhiều Window](https://learn.microsoft.com/en-us/windows/apps/develop/ui/multiple-windows), [AppWindow/presenter](https://learn.microsoft.com/en-us/windows/apps/develop/ui/manage-app-windows), [DisplayArea](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.displayarea?view=windows-app-sdk-1.8).

`QuickWindowEnabled` mặc định true để giữ hành vi dữ liệu cũ. Toggle trong Cài đặt lưu riêng cờ trước khi phát sự kiện áp dụng; khi ghi lỗi, trả công tắc về trạng thái cũ. Khởi động áp dụng sau khi ViewModel load xong. QuickWindow có cờ enabled để không bị gọi mở từ shortcut khi tắt; Disable dừng timer, hủy tác vụ và Hide. Nếu cửa sổ phụ đã tắt, Close main lên lịch thoát ngoài callback Closing để không tạo tiến trình ẩn không thể mở lại.

## Giao diện Framer và theme

MainWindow.Shell.cs quản lý sidebar/transition và theme; MainViewModel/adapter vẫn giữ các lệnh chức năng. DarkThemeEnabled thêm cuối AppSettings, mặc định false cho JSON cũ. Chuyển theme lưu riêng bằng bản sao cài đặt đã lưu để không ghi key hay form còn nhập dở. Thay FrameworkElement.RequestedTheme, không thay Application.RequestedTheme lúc đang chạy hoặc theme Windows.

Theme dictionaries Light/Dark/Default/HighContrast khai báo màu semantic. Alias brush native cho AccentButton và ToggleSwitch đặt tại root để màu nhấn Windows không ghi đè mẫu. Titlebar theo palette. Nền màu mặc định đi theo theme; media và RGB tùy chỉnh không bị xóa.

Sidebar dùng ToggleButton native với shared indicator theo yêu cầu mẫu, thay vì NavigationView có marker khác mẫu. Không có hamburger tự chế. Navigation dùng TranslateTransform/Opacity Storyboard, dọn animation cũ và version completion khi chuyển liên tục; tôn trọng reduced motion/high contrast. Không fetch mạng khi chuyển mục/theme.
