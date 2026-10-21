# Kiểm tra bản đầu — 18/10/2026

## Giao diện Framer, sidebar và sáng/tối (21/10/2026)

- Mở lại mẫu Framer bằng trình duyệt; xem Tạo liên kết, Lịch sử, Cài đặt và chuyển sáng/tối. Dùng mẫu làm tham chiếu giao diện, không đưa nội dung website hoặc endpoint của mẫu vào logic rút gọn.
- Sidebar native, khối chọn chung di chuyển Y và trang fade/slide 140–240 ms. Storyboard cũ được dừng, trạng thái cũ dọn trước khi chuyển tiếp; OS reduced motion/high contrast thì dùng trạng thái tĩnh. Bố cục theo Root.SizeChanged của WinUI Window để xác định đúng kích thước khả dụng.
- Debug build và Release publish thành công ở `artifacts/QH.MultiLink-framer/`, đủ hai XBF và PRI. xUnit **74/74** qua, mở rộng kiểm tra theme mặc định, lưu theme tối qua lần load lại và giữ theme khi đổi màu/cửa sổ nổi.
- Kiểm tra Windows thật với `artifacts/qa-framer`, không dùng key hoặc lịch sử cá nhân: giao diện sáng/tối, sidebar, chuyển Lịch sử có ba kết quả từ smoke công khai, các nút Sao chép/Mở link/QR, danh sách Cài đặt đủ key/provider/nền/cửa sổ nổi, khung hai cột, ghi theme vào SQLite và khôi phục tối khi mở lại.
- Debug QA dùng override chuyển động chỉ trong profile thử, không thay đổi Windows. Release vẫn tôn trọng OS motion. Chưa đo chính xác từng frame hoặc kiểm tra high contrast, nhiều DPI trên máy thật.
- Không tạo link thật trong lần đổi UI này; provider/HTTP/coordinator không đổi. Bộ kiểm thử trước đây vẫn chạy qua. Không xóa, đọc hoặc chỉnh key thật. Screenshot QA lưu trong `artifacts/screenshots/`.

## Công tắc cửa sổ nổi (21/10/2026)

- Thêm Cài đặt → Cửa sổ nổi → Bật cửa sổ nổi. Lưu riêng cờ QuickWindowEnabled ngay khi đổi; không ghi các key hoặc cài đặt khác còn nhập dở. Giá trị được đưa vào snapshot Lưu cài đặt và giữ nguyên khi lưu giao diện.
- Khởi động đợi load settings trước khi show cửa sổ phụ; khi tắt không hiện tab. Tắt lúc đang xử lý sẽ hủy tác vụ, dừng timer và Hide; cập nhật kết quả không tự Show lại. Khi tắt, nút mở cửa sổ nổi trên main bị vô hiệu hóa; đóng main thoát để tránh tiến trình nền không có cửa sổ truy cập.
- xUnit **74/74** qua, bổ sung kiểm tra lưu trạng thái tắt, load lại, đổi màu không làm mất cờ và bật lại. Settings cũ thiếu cờ vẫn mặc định bật.
- Release publish thành công tại `artifacts/QH.MultiLink-edge-settings/`, đủ EXE, hai XBF và PRI.
- Không điều khiển UI trong lần sửa công tắc này; kiểm tra hành vi cửa sổ trực tiếp còn giới hạn như mục bên dưới.

## Cửa sổ nổi độc lập (21/10/2026)

- Bỏ nút Thu gọn và toàn bộ nhánh `_compactMode` khỏi main; thay bằng nút mở cửa sổ nổi. Hai Window có trạng thái nhập/kết quả riêng; dùng chung provider, cài đặt và lịch sử.
- xUnit **73/73** qua: chu trình Collapsed/Input/Loading/Result, validation, mật khẩu chỉ gửi provider hỗ trợ, không failover khi key thiếu, chống Enter trùng, hủy hoàn tất, lịch sử lỗi không mất link, reload ưu tiên và placement/DPI/taskbar bằng kiểm thử tọa độ.
- Debug build và Release publish thành công. Đủ MainWindow.xbf, QuickWindow.xbf và PRI tại `artifacts/QH.MultiLink-edge/`.
- Kiểm tra Windows thật với dữ liệu riêng `artifacts/qa-edge`: có hai cửa sổ khác nhau; tab nhỏ mở khung nhập, URL hợp lệ làm xuất hiện ô mật khẩu; bấm tạo chuyển sang tab loading. Xác nhận SQLite có kết quả spoo.me thành công cho URL mẫu công khai, 2.090 ms, không lưu URL gốc.
- Người dùng dừng Computer Use bằng Escape trong lúc kiểm tra tiếp; không tiếp tục điều khiển UI. Chưa hoàn tất kiểm tra trực tiếp bố cục kết quả, thao tác clipboard, menu mở lại main/thoát, hoặc bản Release đang chạy. Không dùng phương thức UI khác để tiếp tục sau khi bị dừng.
- DPI/độ phân giải khác và high contrast chưa kiểm tra trên màn hình thật. Chỉ placement được kiểm thử bằng dữ liệu giả lập. Tab/loading 44×52, khung mở 281×87 logical px, neo màn hình chính và theo tỷ lệ hiển thị.
- Đóng main chỉ ẩn nó; menu chuột phải trên tab có Mở giao diện chính và Thoát ứng dụng. Thoát hủy/chờ tác vụ ở cả hai cửa sổ trước khi giải phóng host.

## Sửa lưu nhiều key dịch vụ (18/10/2026)

- Nguyên nhân: ô nhập key trước đây bị xóa khi đổi dịch vụ, trong khi key chỉ lưu khi bấm Lưu cài đặt. Không phải giới hạn một key cho toàn ứng dụng.
- Giữ bản nháp riêng theo dịch vụ; lưu riêng bằng nút cạnh trường nhập hoặc lưu tất cả khi Lưu cài đặt. Không tự ghi key chỉ do chuyển lựa chọn.
- Trạng thái cạnh ô key phân biệt chưa lưu, đã lưu mã hóa và đang sửa key cũ; lỗi xác thực/lưu hiển thị tại biểu mẫu. Chỉ xóa phần nhập sau khi ghi thành công.
- xUnit **62/62** qua, gồm chuyển qua lại nhiều bản nháp, lưu riêng, lưu chung, giữ phần nhập khi validation/ghi tệp lỗi, đổi domain giữ key Short.io và đọc nhiều key DPAPI sau khởi tạo lại.
- Build Debug và publish Release thành công. Kiểm tra mã binding sinh ra xác nhận PasswordBox cập nhật ConnectionKey qua PasswordChanged.
- Bản mới xuất riêng tại `artifacts/QH.MultiLink-key-fix/`, giữ bản cũ để tránh gián đoạn phần nhập của người dùng; vẫn dùng chung thư mục dữ liệu thật. Không đọc/ghi key thật hoặc gọi API trong lần sửa này.

## Mở rộng dịch vụ (18/10/2026)

- xUnit **56/56** qua; build Debug và publish Release thành công, không cảnh báo/lỗi.
- Catalog mới gồm spoo.me, da.gd, clck.ru, TinyURL, Dub và Short.io. Không còn is.gd/v.gd trong lựa chọn hoạt động; giữ adapter tham chiếu và lịch sử cũ.
- Kiểm thử hợp đồng các API mới: URL encoding, đúng endpoint/Authorization, không đưa key vào payload/query, chuẩn hóa plain text/JSON, từ chối host hoặc scheme không đúng, không gửi mạng khi thiếu key, cooldown 429 và phản hồi xác nhận mật khẩu.
- Kiểm tra thật qua adapter: 3/3 dịch vụ dùng ngay thành công cho `https://example.com/`. Lần đầu: spoo.me 1.664 ms, da.gd 999 ms, clck.ru 1.160 ms. Lần kiểm tra xuất JSON: tương ứng 860/876/1.061 ms. Chỉ vài mẫu cục bộ, không phải tốc độ bảo đảm.
- Công cụ `--live --json` xuất báo cáo máy đọc được ở `artifacts/smoke/results.json`, không đọc key hoặc lịch sử thật, không mở URL đích.
- Đã kiểm tra UI Debug với profile `artifacts/qa-providers`: menu đủ 6 dịch vụ, chú thích cần key, đổi ưu tiên da.gd cập nhật nhãn màn hình tạo; thẻ kết nối đủ 4 dịch vụ, ô domain xuất hiện khi chọn Short.io.
- Trong chế độ nhiều dịch vụ, checkbox TinyURL/Dub/Short.io bị vô hiệu hóa khi chưa có key. Đã mở thành công bản Release cập nhật từ thư mục publish; mặc định spoo.me hiển thị đúng và đủ tài nguyên PRI/XBF.
- TinyURL, Dub và Short.io chưa được gọi bằng key thật. Kiểm thử giả lập không xác nhận quota, gói tài khoản hoặc tốc độ thực tế của ba dịch vụ này.
- Kết quả khảo sát và nguồn chính thức: [PROVIDER_RESEARCH.md](PROVIDER_RESEARCH.md). Các số liệu is.gd/v.gd trong phần kiểm tra bản đầu bên dưới là lịch sử, không phản ánh trạng thái hiện tại.

## Cập nhật nền và cửa sổ tối giản

- xUnit: **35/35** qua; build Debug không cảnh báo/lỗi.
- Kiểm tra dịch vụ ưu tiên, provider tương thích mật khẩu, giữ cài đặt qua lần mở sau và đọc dữ liệu settings cũ.
- Kiểm tra mốc 15 giây: nhận đúng 15, từ chối trên 15 giây; GIF kiểm tra toàn bộ các khung, không chỉ khung đầu; ghi lại lặp vô hạn mà không đổi dữ liệu hình ảnh.
- Windows decoder thật: nhập GIF, MP4 1 giây, từ chối MP4 16 giây, giữ nguyên tệp gốc và nền trước đó khi nhập thất bại.
- Đã kiểm tra cửa sổ thường 900 × 760, cửa sổ gọn 660 × 560, chọn nền/mặc định trong Cài đặt, và GIF phát/dừng với cấu hình QA tách riêng dữ liệu.
- Đã xác nhận MP4 mẫu hiển thị và phát trong MediaPlayerElement, không có thanh điều khiển video; các khối nhập/kết quả vẫn có nền riêng, đọc rõ trên nền động.
- Bản Release cập nhật đã mở thành công, giao diện mới và chế độ thu gọn hiển thị đúng. Đổi ưu tiên sang v.gd trong hồ sơ QA đã được ghi vào SQLite và nhãn tạo link cập nhật theo.
- Đã kiểm tra fallback màu nền khi Windows tắt chuyển động. Chế độ QA Debug có thể mô phỏng motion để kiểm tra playback; không thay đổi thiết lập Windows, không có override này trong Release.

Lệnh hỗ trợ QA cục bộ (không gọi provider):

```powershell
dotnet run --project tools/QH.SmokeCheck -- --media-fixtures
dotnet run --project tools/QH.SmokeCheck -- --qa-gif
dotnet run --project tools/QH.SmokeCheck -- --qa-mp4
```

Dùng Debug với `QH_TEST_DATA_DIRECTORY` trỏ đến `artifacts/qa-appearance` để tách biệt dữ liệu thật. `QH_TEST_ENABLE_MOTION=1` mô phỏng motion chỉ trong profile QA Debug.

## Kiểm tra nền tảng bản đầu

- .NET 10.0.402 trên Windows x64.
- Build WinUI: không cảnh báo, không lỗi.
- xUnit: **22/22** qua, gồm concurrency, stream kết quả, lỗi độc lập, hủy, timeout, validation, mã hóa DPAPI, SQLite, không retry POST và cooldown rate limit.
- API thật với URL công khai `https://example.com/`: is.gd, v.gd và spoo.me đều thành công. Không dùng API key cá nhân; không truy cập URL đích.
- spoo.me với mật khẩu thử được sinh ngẫu nhiên: API chấp nhận, trả link. Trạng thái hiển thị Requested; chưa xác minh độc lập bằng cách mở link.
- QR PNG được sinh cục bộ từ ba link thử.
- Đã mở bản Debug, kiểm tra màn hình tạo link, báo lỗi URL rỗng, điều hướng và trang cài đặt qua công cụ Windows.
- Bản publish cần copy tệp PRI/XBF của ứng dụng; đã bổ sung vào quy trình xuất bản để không mất tài nguyên XAML.
- Đã mở thành công bản Release self-contained trong `artifacts/QH.MultiLink/`, kiểm tra bố cục sáng và màu xanh đồng nhất trên nút/checkbox. Tài nguyên Light và high contrast được khai báo độc lập; high contrast chưa kiểm tra bằng UI.

Chưa kiểm tra trên Windows 10, nhiều DPI, Accessibility Insights/Narrator, chế độ high contrast hoặc RAM qua benchmark. Chưa có bộ cài/chữ ký số.

## Chạy lại

```powershell
dotnet test tests/QH.MultiLinkShortener.Tests
dotnet run --project tools/QH.SmokeCheck -- --live
dotnet run --project tools/QH.SmokeCheck -- --live --protected
```

Hai lệnh smoke có tác động thật: tạo link công khai cho example.com. Smoke dùng bí mật giả/ẩn danh, không đọc hoặc ghi lịch sử/key của ứng dụng. Lệnh `--live` sinh PNG trong `artifacts/smoke`. Lệnh `--protected` tạo mật khẩu ngẫu nhiên chỉ trong bộ nhớ, không ghi ra output.
