# QH Multi-Link Shortener

Ứng dụng desktop Windows 10/11, C#/.NET 10 và WinUI 3. Rút gọn một URL qua nhiều dịch vụ, nhận kết quả riêng ngay khi hoàn tất.

## Chạy ứng dụng

Tải **QH.MultiLink-2.0.0-win-x64.zip** tại [GitHub Release v2.0.0](https://github.com/Quhuy007/QH-MultiLink/releases/tag/v2.0.0). Giải nén toàn bộ rồi mở `QH.MultiLinkShortener.App.exe`. Giữ toàn bộ các tệp đi kèm; có sẵn .NET và Windows App SDK, không cần API key để bắt đầu. Đây là bản portable, chưa có chữ ký số.

Nếu đã dùng bản cũ, ứng dụng tiếp tục dùng dữ liệu cục bộ của bạn. Gói tải về không chứa API key, cấu hình cá nhân hoặc lịch sử của người phát triển.

Yêu cầu Windows 10 phiên bản 1809 trở lên hoặc Windows 11, máy x64, và Internet khi tạo link. Mã QR và lịch sử hoạt động cục bộ.

## Sử dụng

1. Dán URL và bấm **Tạo link** hoặc nhấn **Enter**. Không phải xác nhận dịch vụ mỗi lần.
2. Vào **Cài đặt → Dịch vụ mặc định** để chọn spoo.me, da.gd, clck.ru, TinyURL, Dub hoặc Short.io, rồi **Lưu cài đặt**. Ba dịch vụ sau cần kết nối key trước. Mặc định ban đầu là spoo.me. Không tự gửi sang dịch vụ khác nếu dịch vụ ưu tiên bị lỗi.
3. Nhấn **Cửa sổ nổi** hoặc tab xanh ở mép phải để mở cửa sổ tạo nhanh độc lập. Giao diện chính giữ kích thước bình thường; đã bỏ chế độ Thu gọn cũ.
4. **Tùy chọn nâng cao** chứa mật khẩu và chế độ nhiều dịch vụ. Nếu cần mật khẩu, ứng dụng chọn dịch vụ đã cấu hình có hỗ trợ (spoo.me, Dub, Short.io). Dịch vụ thực tế được hiển thị cạnh ô URL. Mật khẩu cần ít nhất 8 ký tự có chữ, số và ký tự đặc biệt.
5. Chú thích URL chỉ hiện khi rê chuột hoặc dùng bàn phím tập trung vào ô URL. Các lỗi nhập liệu vẫn hiển thị ngay dưới trường cần sửa.
6. Sao chép link, mở bằng trình duyệt, xuất QR PNG hoặc xem **Lịch sử**. Có thể hủy tác vụ; hủy/timeout không đảm bảo dịch vụ chưa tạo link ở phía máy chủ.

Phím truy cập: Alt+U tới URL, Alt+S hoặc Enter để tạo link. Các điều khiển hỗ trợ Tab và bàn phím.

## Giao diện theo mẫu Framer

- Mẫu tham chiếu: [qhshortlink.framer.website](https://qhshortlink.framer.website/), được mở lại và đối chiếu ba mục và hai chế độ màu.
- Sidebar bên trái với ba mục Tạo liên kết / Lịch sử / Cài đặt. Một khối chọn dùng chung trượt giữa các mục; nội dung fade/slide nhẹ, có xử lý khi chuyển liên tiếp. Windows tắt chuyển động hoặc bật high contrast thì chuyển tức thời.
- Công tắc sáng/tối ở cuối sidebar áp dụng và tự lưu ngay; mở lại app giữ lựa chọn. Không đổi theme Windows. Mặc định sáng cho dữ liệu cũ.
- Khung tạo và kết quả nằm cạnh nhau khi đủ rộng; tự xếp dọc khi hẹp. Sidebar rút thành biểu tượng dưới 760 logical px. Không có chế độ thu gọn cửa sổ chính.
- Giữ đầy đủ tính năng nâng cao, mật khẩu, nhiều provider, hủy, lịch sử, clipboard, QR, key mã hóa, nền màu/GIF/MP4 và bật/tắt cửa sổ nổi. Đổi theme không ghi các key đang nhập dở.
- Màu nền mặc định chuyển theo theme; màu tùy chỉnh và media đã chọn vẫn được giữ. Khối form/kết quả giữ nền riêng để chữ dễ đọc.
- Ảnh giao diện: [Chế độ sáng](docs/images/light.jpg) · [Chế độ tối](docs/images/dark.jpg).

## Cửa sổ nổi ở mép phải

- Vào **Cài đặt → Cửa sổ nổi → Bật cửa sổ nổi** để bật/tắt. Áp dụng và lưu tự động, nhớ trạng thái qua lần mở lại. Tắt sẽ ẩn hoàn toàn khung/tab và hủy tác vụ của cửa sổ phụ; nút mở cửa sổ nổi trên thanh đầu cũng bị vô hiệu hóa. Cài đặt cũ chưa có lựa chọn này mặc định bật.
- Là một WinUI Window riêng, không phải một vùng nằm trong cửa sổ chính; nổi trên các cửa sổ thông thường, không thêm mục riêng vào taskbar/Alt+Tab.
- Khi khởi động chỉ hiện tab nhỏ **44 × 52**. Nhấn tab hoặc nút **Cửa sổ nổi** trên app để mở khung **281 × 87** ở mức DPI 100%. Windows tăng kích thước theo tỷ lệ hiển thị để giữ chữ dễ đọc.
- Neo mép phải vùng làm việc của màn hình chính, vị trí Y tham chiếu 779; tự đưa lên nếu vượt mép dưới/taskbar. Không cố định X=1580 vì độ rộng màn hình có thể khác bản phác thảo.
- Nhập/dán URL; URL hợp lệ làm xuất hiện ô mật khẩu tùy chọn bên dưới. Enter hoặc nút mũi tên tạo link; Tab chuyển tới ô mật khẩu. Không có mật khẩu thì Enter ngay.
- Đang xử lý: khung thu lại thành ô loading. Nhấn ô loading hoặc Escape để hủy; dịch vụ có thể đã nhận yêu cầu trước khi hủy.
- Thành công: dòng 1 là link, dòng 2 là **Sao chép / Ẩn**. Sao chép giữ kết quả trên màn hình; Ẩn hoặc Escape quay về tab và xóa phần nhập hiện tại.
- Lỗi: hiện thông báo ngắn với **Sửa lại / Mở app / Ẩn**; tooltip cho biết đầy đủ lỗi. Không tự chuyển dịch vụ sau lỗi.
- Dùng dịch vụ mặc định đã lưu; khi có mật khẩu chọn dịch vụ tương thích như giao diện chính. Lịch sử dùng chung, mật khẩu không được lưu.
- Khi cửa sổ nổi được bật, thu nhỏ hoặc đóng cửa sổ chính vẫn giữ tab nổi. Nhấn chuột phải vào tab → **Mở giao diện chính** để mở lại, hoặc **Thoát ứng dụng** để kết thúc cả hai cửa sổ và hủy tác vụ đang chạy. Khi cửa sổ nổi đã tắt, đóng cửa sổ chính sẽ thoát ứng dụng.

## Màu nền và nền động

Trong **Cài đặt → Màu sắc và nền động**:

- **Chọn màu nền**: bộ chọn màu RGB/HEX, lưu tự động sau khi xác nhận màu.
- **Chọn GIF / MP4**: chỉ nhận tệp cục bộ có thời lượng một vòng **lớn hơn 0 và không quá 15 giây**, tối đa **20 MiB**, độ phân giải tối đa **1920 × 1080** hoặc dọc tương đương. GIF tối đa 450 khung hình.
- Tệp dài hơn bị từ chối, không tự cắt video. GIF được chuẩn hóa phát lặp; MP4 phát lặp và luôn tắt tiếng.
- Nút **Dừng nền / Phát nền** có trong Cài đặt và trên thanh đầu cửa sổ (biểu tượng pause/play).
- **Bỏ nền động** trở về màu nền đã chọn. Màu và nền được giữ sau khi mở lại ứng dụng.
- Tạm ngừng chuyển động khi cửa sổ không hoạt động; dùng nền tĩnh khi Windows tắt animation hoặc bật high contrast.
- Ứng dụng sao chép tệp vào `%LOCALAPPDATA%\QH\MultiLink\Backgrounds`, không sửa tệp gốc hoặc tải lên dịch vụ nào. Khối nội dung giữ nền riêng để chữ luôn dễ đọc.

## Dịch vụ

| Dịch vụ | API key | Mật khẩu | API chính thức |
|---|---|---|---|
| spoo.me v1 | Tùy chọn | Có | [Tài liệu](https://spoo.me/docs/api-reference/url-shortening/create-shortened-url) |
| da.gd | Không cần | Không | [Tài liệu](https://da.gd/help) |
| clck.ru | Không cần | Không | [API trên trang chủ](https://clck.ru/) |
| TinyURL | Bắt buộc | Không trong app | [Tài liệu](https://tinyurl.com/app/dev) |
| Dub | Bắt buộc | Có | [Tài liệu](https://dub.co/docs/api-reference/links/create) |
| Short.io | Public API key + domain | Có | [Tài liệu](https://developers.short.io/reference/post_links-public) |

is.gd và v.gd đã tạm gỡ khỏi danh sách hoạt động. Cấu hình ưu tiên cũ tự đổi sang spoo.me kèm thông báo; lịch sử được giữ. Đã thử trực tiếp thành công ba dịch vụ dùng ngay; ba dịch vụ cần key mới chỉ được kiểm thử hợp đồng API giả lập. Xem [khảo sát, tốc độ đo và hướng dẫn kết nối](PROVIDER_RESEARCH.md).

spoo.me v1 nhận mật khẩu nhưng không trả trường xác nhận bảo vệ riêng. Ứng dụng hiển thị **Đã gửi mật khẩu theo API**, không khẳng định đã kiểm chứng link. Không truy cập URL đích hoặc tự thử mở link sau khi tạo.

Các API có quota riêng. Không tự retry yêu cầu tạo link, kể cả GET, để tránh tạo trùng. Khi bị rate limit, adapter tạm ngừng gửi thêm; lỗi mạng liên tiếp kích hoạt circuit breaker riêng theo dịch vụ. clck.ru chỉ nhận URL tối đa 2.000 ký tự.

## Dữ liệu và bảo mật

- Lịch sử và cài đặt: `%LOCALAPPDATA%\QH\MultiLink\history.db`.
- API key: `spoo.secret`, `tinyurl.secret`, `dub.secret`, `shortio.secret` trong cùng thư mục, mã hóa DPAPI CurrentUser. Cấu hình Short.io gồm key và domain được mã hóa chung. Không ghi key trong DB, source hoặc log; không hiện lại key đã lưu.
- Kết nối dịch vụ: phần nhập dở của mỗi dịch vụ được giữ riêng khi chuyển lựa chọn. **Lưu key dịch vụ này** lưu riêng; **Lưu cài đặt** lưu mọi key đang chờ. Dòng trạng thái phân biệt **Chưa lưu thay đổi** với **Đã lưu key mã hóa**. Sau khi lưu, ô key trống để giữ key hiện tại; phần nhập chưa lưu chỉ tồn tại cho tới khi đóng app.
- Không lưu mật khẩu của link; URL gốc mặc định không lưu. Lịch sử vẫn chứa link ngắn, dịch vụ, trạng thái và thời gian.
- Chỉ gửi dữ liệu tới provider được chọn qua HTTPS; không chuyển tiếp redirect API; không bỏ qua kiểm tra chứng thư.
- Không ghi log HTTP hoặc thông báo lỗi thô từ provider để tránh lộ token/URL/mật khẩu.
- QR được tạo bằng QRCoder cục bộ. Nó chỉ chứa link ngắn.
- **Xóa lịch sử** chỉ xóa dữ liệu cục bộ. Các link trên dịch vụ không bị xóa. Đổi tùy chọn lưu URL gốc không xóa bản ghi cũ.

## Phát triển

Cần .NET 10 SDK, Windows x64 và truy cập NuGet. Dự án sử dụng Windows App SDK 1.8 stable, self-contained, unpackaged.

```powershell
dotnet build src/QH.MultiLinkShortener.App -p:Platform=x64
dotnet test tests/QH.MultiLinkShortener.Tests
dotnet publish src/QH.MultiLinkShortener.App -c Release -p:Platform=x64 -o artifacts/QH.MultiLink
```

- `App`: giao diện tiếng Việt, CommunityToolkit.Mvvm, Generic Host, clipboard/QR/file picker.
- `Core`: hợp đồng provider, kiểm tra dữ liệu và điều phối có giới hạn, stream trạng thái, hủy/timeout.
- `Infrastructure`: HTTP factory/resilience, adapter từng dịch vụ, SQLite, DPAPI và QR.
- `Tests`: điều phối, hủy, timeout, không truyền mật khẩu sai provider, encoding API, phản hồi lỗi an toàn, lưu trữ và mã hóa.
- `design-system/qh-multi-link/MASTER.md`: hướng thiết kế đã áp dụng từ skill ui-ux-pro-max.

Bộ kiểm thử xUnit không tạo link thật. Kiểm thử hợp đồng API dùng HTTP giả lập; kiểm tra với dịch vụ thật phụ thuộc mạng, quota và chính sách nhà cung cấp.

Kết quả kiểm tra bản đầu và lệnh smoke dùng URL công khai: xem [VALIDATION.md](VALIDATION.md). Trước khi phát hành bản mới có thể dùng `dotnet clean` rồi publish để tạo lại toàn bộ tài nguyên giao diện.

Tài liệu media: [BitmapImage và GIF](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.media.imaging.bitmapimage?view=windows-app-sdk-1.8), [MediaPlayer cho WinUI](https://learn.microsoft.com/en-us/windows/apps/develop/media-playback/play-audio-and-video-with-mediaplayer).

## Nguồn API đã đối chiếu (18/10/2026)

- [is.gd API](https://is.gd/apishorteningreference.php), [giới hạn](https://is.gd/usagelimits.php), [điều khoản](https://is.gd/terms.php).
- [v.gd API](https://v.gd/apishorteningreference.php), [giới hạn](https://v.gd/usagelimits.php), [điều khoản](https://v.gd/terms.php).
- [spoo.me tạo link](https://spoo.me/docs/api-reference/url-shortening/create-shortened-url), [API key](https://spoo.me/docs/api-keys), [giới hạn](https://spoo.me/docs/rate-limits), [điều khoản](https://spoo.me/terms).

Chưa đo RAM trên máy Windows 10 hoặc kiểm thử trên nhiều cấu hình/DPI. Bản đầu chưa có bộ cài hay chữ ký số.
