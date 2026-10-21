# QH Multi-Link v2.0.0

Ứng dụng rút gọn liên kết cho Windows với giao diện tiếng Việt.

## Có trong bản này

- Giao diện mới với sidebar, chuyển mục có hiệu ứng, chế độ sáng/tối được ghi nhớ.
- Cửa sổ nổi độc lập ở mép phải: nhập link, mật khẩu tùy chọn, tạo và sao chép nhanh. Bật/tắt trong Cài đặt.
- Dịch vụ ưu tiên mặc định, tùy chọn nhiều dịch vụ, hủy tác vụ và nhận kết quả từng dịch vụ.
- spoo.me, da.gd, clck.ru; kết nối TinyURL, Dub và Short.io bằng API key riêng. is.gd/v.gd đã gỡ khỏi danh sách hoạt động.
- Giữ phần nhập key riêng cho từng dịch vụ; key đã lưu được mã hóa bằng Windows DPAPI.
- Lịch sử cục bộ, xuất mã QR PNG, màu nền tùy chỉnh và GIF/MP4 lặp tối đa 15 giây.

## Tải và chạy

1. Tải **QH.MultiLink-2.0.0-win-x64.zip** trong Assets bên dưới.
2. Giải nén toàn bộ thư mục.
3. Chạy **QH.MultiLinkShortener.App.exe**.

Yêu cầu Windows 10 1809 trở lên hoặc Windows 11, x64. Gói có sẵn .NET và Windows App SDK. Đây là bản portable chưa ký số. Không chạy EXE trực tiếp bên trong ZIP.

API key, lịch sử và nền cá nhân không được đính kèm. Bản cũ và bản này dùng chung dữ liệu ở `%LOCALAPPDATA%\QH\MultiLink`.

## Kiểm tra và giới hạn

- 74/74 kiểm thử tự động đạt trước khi phát hành.
- Ba dịch vụ không cần key đã được thử trực tiếp trong quá trình phát triển; các dịch vụ cần key được kiểm thử bằng phản hồi API giả lập, chưa kiểm chứng bằng key cá nhân.
- Dịch vụ bên ngoài có thể thay đổi quota, tốc độ hoặc ngừng hoạt động. Hủy yêu cầu không bảo đảm dịch vụ chưa tạo link.
- Tệp **SHA256SUMS.txt** dùng để kiểm tra tính toàn vẹn của ZIP.
