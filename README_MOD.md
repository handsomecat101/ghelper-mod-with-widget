# G-Helper — Custom Mod (Floating Monitor + Network)

> **Dự án gốc / Original project:** [seerge/g-helper](https://github.com/seerge/g-helper)  
> Đây là một **bản fork không chính thức** được mod thêm tính năng. Mọi tính năng cốt lõi, license và quyền tác giả thuộc về [seerge](https://github.com/seerge).

---

## 🆕 Tính năng mod thêm

### 🪟 Floating Monitor Widget
Widget nổi hiển thị thông số máy theo thời gian thực, kéo thả tự do trên màn hình.

| Thông số | Mô tả |
|----------|-------|
| **CPU** | Công suất (W) · Nhiệt độ (°C) · % sử dụng |
| **GPU** | Công suất (W) · Nhiệt độ (°C) · % sử dụng (hiện `OFF` khi không hoạt động) |
| **FANS** | Tốc độ quạt CPU \| GPU \| Mid (RPM) |
| **Battery** | Công suất sạc/xả (W) · Ước tính thời gian còn lại (h m) |
| **MODE** | Chế độ hiệu năng hiện tại (BALANCED / TURBO / SILENT) |
| **🌐 Network** | Tên WiFi · ↓ Download · ↑ Upload tốc độ realtime |

### 🎨 Giao diện
- **Glassmorphism** – Nền kính mờ gradient, bo tròn góc
- **Full Theme Sync** – Toàn bộ widget đổi màu theo chế độ:
  - 🔴 **Turbo** → nền đỏ thẫm
  - 🔵 **Balanced** → nền xanh dương
  - 🟢 **Silent** → nền xanh lá
- **Sparklines** – Biểu đồ lịch sử 50 giây cho CPU & GPU power
- **Compact Mode** – Chuột phải để thu gọn widget, hiện thông tin pin + mạng

### 🖱️ Tương tác
- **Kéo thả** – Tự do đặt widget bất kỳ vị trí nào, vị trí được lưu tự động
- Click vùng **MODE** → chuyển chế độ hiệu năng ngay lập tức
- Click vùng **FANS** → mở tab cài đặt quạt trong G-Helper

### 🌐 Network Monitor (mới)
- Hiển thị **tên WiFi (SSID)** đang kết nối  
- Dot **🟢 xanh** = online, **🔴 đỏ** = offline  
- **↓ xanh lá** = tốc độ Download, **↑ cam đỏ** = tốc độ Upload  
- Lọc adapter ảo/VPN, smoothing để tránh số nhảy loạn

### 🔧 Sửa lỗi
- Fix đọc **quạt 0 RPM** trên ROG Flow Z13 và một số model dòng Z
- Fix đọc **TDP Intel** qua WinRing0 driver (cần chạy Administrator)
- Fix **vị trí widget nhảy** khi kéo thả

---

## 📁 Files thay đổi so với bản gốc

| File | Thay đổi |
|------|----------|
| `app/UI/MonitorForm.cs` | **Mới** – Toàn bộ Floating Monitor widget |
| `app/Helpers/NetworkControl.cs` | **Mới** – Đọc WiFi SSID, online status, tốc độ mạng |
| `app/HardwareControl.cs` | Thêm: đọc TDP Intel, tích hợp NetworkControl |
| `app/Helpers/OSDBase.cs` | Cho phép tương tác chuột với cửa sổ OSD |
| `app/AsusACPI.cs` | Cải thiện logic đọc RPM quạt |

---

## 🚀 Cách dùng

1. Download bản build từ [Releases](../../releases)
2. Giải nén, chạy `GHelper.exe` (nên **Run as Administrator** để đọc TDP)
3. Vào **Extra** → bật **Floating Monitor**
4. Widget xuất hiện ở góc trên phải, kéo thả để đặt vị trí tuỳ thích

---

## ⚙️ Build từ source

**Yêu cầu:** .NET 8 SDK

```bash
cd app
dotnet publish GHelper.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

---

## 📜 License & Credits

- **Dự án gốc:** [seerge/g-helper](https://github.com/seerge/g-helper) — License: [GPL-3.0](LICENSE)
- **Bản mod này:** Giữ nguyên license GPL-3.0
- Mọi đóng góp cho dự án gốc xin vui lòng gửi về repo chính thức của **seerge**
