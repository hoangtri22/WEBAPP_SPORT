# SPORTSHOP – Hệ thống bán hàng và quản lý khách hàng

Hệ thống cho một cửa hàng đồ thể thao, gồm hai web, một app Android và một REST API dùng chung CSDL MySQL.

| Phần | Người dùng | Cổng | Tài liệu |
|---|---|---|---|
| Web Admin | Quản lý, Nhân viên | 5188 | [Frontend/SportShop.Admin](Frontend/SportShop.Admin/README.md) |
| Web bán hàng | Khách hàng | 5003 | [Frontend/SportShop.CustomerWeb](Frontend/SportShop.CustomerWeb/README.md) |
| App bán hàng (Android) | Khách hàng | – | [MobileApp](MobileApp/README.md) |
| API/Backend và CSDL | App gọi vào | 5080 | [Backend/SportShop.Api](Backend/SportShop.Api/README.md) |

## Kiến trúc

```text
Web Admin    ──> ShopData, ShopCommands (trong SportShop.Admin) ──────> MySQL
Web bán hàng ──> SportShop.Shared ────────────────────────────────────> MySQL
App Android  ──HTTP──> SportShop.Api ──> SportShop.Shared ────────────> MySQL
                                     └─> SportShop.Infrastructure ────> MySQL
```

- App chỉ gọi API, không kết nối MySQL.
- `SportShop.Shared` biên dịch chung thư mục `Services` và `Models` của Web bán hàng, nên Web bán hàng và API chạy cùng một bản nghiệp vụ. Web Admin tham chiếu `SportShop.Shared` nhưng có tầng dữ liệu riêng.
- Ảnh sản phẩm nằm ở `Backend/Images` (đường dẫn `/images`), ảnh tải lên từ Web Admin nằm ở `Frontend/Uploads` (`/uploads`). Hai web và API phục vụ cùng hai thư mục này.

## Công nghệ

| Phần | Công nghệ |
|---|---|
| Hai web | ASP.NET Core MVC (.NET 10), Razor |
| API | ASP.NET Core (.NET 10), OpenAPI |
| App | .NET MAUI (`net10.0-android`, Android 7.0 trở lên) |
| Dữ liệu | MySQL 8, Dapper, MySqlConnector |
| Xuất file | ClosedXML (Excel), QuestPDF (PDF) |
| Kiểm thử | xUnit |

## Cấu trúc thư mục

```text
WEBAPP_SPORT/
├── Backend/
│   ├── Database/               # Script tạo CSDL và các migration
│   ├── Images/                 # Ảnh sản phẩm gốc
│   ├── SportShop.Api/          # REST API cho app
│   ├── SportShop.Application/  # Interface của API
│   ├── SportShop.Contracts/    # DTO dùng chung API và app
│   ├── SportShop.Infrastructure/ # Đọc MySQL cho API
│   ├── SportShop.Shared/       # Nghiệp vụ dùng chung Web bán hàng và API
│   ├── SportShop.Api.Tests/    # Test API
│   ├── docs/                   # OpenAPI đã xuất
│   └── scripts/                # Script chạy test API
├── Frontend/
│   ├── SportShop.Admin/        # Web Admin
│   ├── SportShop.CustomerWeb/  # Web bán hàng
│   └── Uploads/                # Ảnh và báo cáo sinh ra khi chạy (git bỏ qua)
├── MobileApp/
│   ├── SportShop.Mobile/       # App Android
│   ├── SportShop.Mobile.Tests/ # Test app
│   └── run-android.ps1         # Build, cài và mở app
├── .vscode/                    # Task và extension gợi ý cho VS Code
└── SportShop.slnx              # Solution
```

## Chuẩn bị (làm một lần)

Yêu cầu: .NET 10 SDK và MySQL 8. Chạy app cần thêm workload .NET MAUI (Android), Android SDK và JDK 21.

1. **Tạo CSDL:** mở MySQL Workbench, kết nối tới server, chọn File → Open SQL Script → `Backend/Database/sports_shop.sql`, bấm ⚡
   (Ctrl + Shift + Enter, chạy cả script). Kết quả đúng là schema `sports_shop` có **35 bảng**.
2. **Chạy migration:** nhấp đúp schema `sports_shop` để đặt làm mặc định, rồi chạy lần lượt các file `001` đến `009` trong `Backend/Database/Migrations`.
   Kết quả đúng là **43 bảng**. Nếu file `007` báo `Error Code: 1175`, chạy `SET SQL_SAFE_UPDATES = 0;` rồi chạy lại file đó.
3. **Cấu hình kết nối:** tạo file `appsettings.Local.json` đặt cạnh `appsettings.json` của `Frontend/SportShop.Admin`, `Frontend/SportShop.CustomerWeb` và `Backend/SportShop.Api`
   (API bắt buộc phải có file này thì app mới tải được dữ liệu). Git bỏ qua file này nên mật khẩu không bị đẩy lên GitHub:

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Port=3306;Database=sports_shop;User=root;Password=MAT_KHAU_CUA_BAN;"
  }
}
```

**Mỗi lần bật máy:** nếu MySQL chưa chạy, mở PowerShell bằng quyền quản trị (Run as administrator) rồi chạy:

```powershell
net start MySQL80
```

| Thành phần | Địa chỉ |
|---|---|
| Web quản trị | [http://localhost:5188](http://localhost:5188) |
| Web khách hàng | [http://localhost:5003](http://localhost:5003) |
| API cho app | [http://localhost:5080/health/ready](http://localhost:5080/health/ready) (thấy `ready` là đã nối được CSDL) |

---

## 🟣 Hướng dẫn chạy bằng Visual Studio

Cần Visual Studio 2026 có hai workload **ASP.NET and web development** và **.NET Multi-platform App UI development**. Mở file `SportShop.slnx`.

### 1. Chạy web

1. Chuột phải vào **Solution 'SportShop'** → **Configure Startup Projects...**
2. Chọn **Multiple startup projects**, đặt **Start** cho `SportShop.Admin` và `SportShop.CustomerWeb`, cột Debug Target chọn **http** (tránh lỗi chứng chỉ SSL).
3. Bấm **F5** (có Debug) hoặc **Ctrl + F5** (chạy thẳng). Trình duyệt mở cả hai web.

### 2. Chạy app Android

1. Vào **Tools → Android → Android Device Manager**, bấm **Start** ở máy ảo và chờ hiện màn hình chính.
   Dùng điện thoại thật thì bật **Gỡ lỗi USB**, cắm cáp và bấm **Cho phép** trên điện thoại.
2. Nối cổng để app gọi được API (làm lại mỗi lần mở máy ảo hoặc cắm lại cáp):

```powershell
& "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe" reverse tcp:5080 tcp:5080
```

3. Chuột phải vào `SportShop.Api` → **Debug → Start Without Debugging**, mở địa chỉ API ở bảng trên để kiểm tra.
4. Chuột phải vào `SportShop.Mobile` → **Set as Startup Project**.
5. Ở nút chạy trên thanh công cụ, chọn máy ảo hoặc điện thoại, rồi bấm **F5**. Lần đầu build mất vài phút.

### 3. Chạy web và app cùng lúc

Làm bước 1, 2 của mục 2, và chọn máy ảo hoặc điện thoại cho `SportShop.Mobile` một lần theo bước 4, 5 của mục 2 (Visual Studio ghi nhớ thiết bị này).
Sau đó vào **Configure Startup Projects**, đặt **Start** cho `SportShop.Api` (để đầu danh sách), `SportShop.Admin`, `SportShop.CustomerWeb` với Debug Target **http**,
và `SportShop.Mobile` với Debug Target **SportShop.Mobile**, rồi bấm **F5**.

**Dừng:** bấm **Shift + F5**. Project mở bằng Ctrl + F5 hoặc Start Without Debugging thì đóng cửa sổ console của nó.

---

## 🔵 Hướng dẫn chạy bằng Visual Studio Code

Mở thư mục `WEBAPP_SPORT` trong VS Code và cài extension **C# Dev Kit**, **.NET MAUI** theo gợi ý của dự án. Các lệnh dưới đây chạy trong Terminal PowerShell (Ctrl + `) tại thư mục này.

### 1. Chạy web

**Terminal 1 (Web quản trị):**

```powershell
dotnet run --project Frontend/SportShop.Admin --launch-profile http
```

**Terminal 2 (Web khách hàng):**

```powershell
dotnet run --project Frontend/SportShop.CustomerWeb --launch-profile http
```

Mở hai địa chỉ web ở bảng trên. Dừng web bằng **Ctrl + C** trong từng terminal.

**Chạy ngầm** (đóng terminal web vẫn chạy):

```powershell
Start-Process dotnet -ArgumentList "run --project Frontend/SportShop.Admin --launch-profile http" -WindowStyle Hidden
Start-Process dotnet -ArgumentList "run --project Frontend/SportShop.CustomerWeb --launch-profile http" -WindowStyle Hidden
```

Tắt web và API đang chạy ngầm (chỉ tắt chương trình giữ 3 cổng của dự án):

```powershell
Get-NetTCPConnection -LocalPort 5188,5003,5080 -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique | ForEach-Object { Stop-Process -Id $_ -Force }
```

### 2. Chạy app Android

1. Mở máy ảo, hoặc cắm điện thoại đã bật **Gỡ lỗi USB**. Xem tên máy ảo bằng tham số `-list-avds`, ví dụ máy ảo `Pixel_5`:

```powershell
Start-Process "C:\Program Files (x86)\Android\android-sdk\emulator\emulator.exe" -ArgumentList "-avd Pixel_5"
```

2. Khi máy ảo hiện màn hình chính, chạy:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\MobileApp\run-android.ps1 -AndroidSdkDirectory "C:\Program Files (x86)\Android\android-sdk"
```

Máy đã cho phép chạy script PowerShell và chỉ có một Android SDK thì có thể bấm **Ctrl + Shift + B** thay cho lệnh trên.

Script tự build app, chạy API nếu chưa có, nối cổng 5080, cài và mở app. Chạy lại bản đã build thì thêm `-SkipBuild`; có nhiều thiết bị thì thêm `-DeviceSerial SERIAL` (xem bằng `adb devices`).

Chỉ chạy riêng API: **Terminal → Run Task → `Backend: Chạy REST API (port 5080)`**.

### 3. Chạy web và app cùng lúc

Chạy hai terminal ở mục 1, rồi làm mục 2.

### 📱 Mẹo: mở web trên điện thoại bằng Dev Tunnels

Để vừa mở web trên laptop, vừa mở trên điện thoại (dùng 4G hoặc Wi-Fi bất kỳ) mà không cần cấu hình IP:

1. Nhìn xuống bảng điều khiển dưới chân VS Code ➔ Chọn tab **PORTS** (Cổng).
2. Bấm **Forward a Port** ➔ Nhập `5003` ➔ Bấm Enter.
3. Chuột phải vào dòng port `5003` ➔ Chọn **Port Visibility** ➔ Chọn **Public**.
4. Copy link HTTPS ở cột **Forwarded Address** (dạng `https://xxxx-5003.asse.devtunnels.ms`) gửi sang điện thoại và mở bằng trình duyệt (bấm nút **Continue** ở màn hình xác thực đầu tiên là vào web ngay).

---

## Lỗi thường gặp

| Hiện tượng | Cách xử lý |
|---|---|
| Web Admin báo "Không kết nối được MySQL" | Chạy `net start MySQL80`, kiểm tra cổng và mật khẩu trong `appsettings.Local.json` |
| `Table '...' doesn't exist` hoặc `Unknown column '...'` | Chạy lại các migration còn thiếu trong `Backend/Database/Migrations` |
| Mã nhân viên ở góc phải sai sau khi tạo lại CSDL | Mã nằm trong cookie cũ; đăng xuất rồi đăng nhập lại |
| Build báo không chép được file `.exe` hoặc `.dll` | Dừng web hoặc API đang chạy rồi build lại |
| `/health/ready` không báo `ready` | Kiểm tra `appsettings.Local.json` của API, MySQL và migration, rồi chạy lại API |
| App không tải được sản phẩm | Kiểm tra API đang chạy, rồi chạy lại lệnh `adb reverse` |
| `adb: more than one device` | Chỉ để lại một thiết bị, hoặc truyền `-DeviceSerial` cho script |
| Cổng 5188, 5003 hoặc 5080 đang dùng | Dừng lần chạy cũ rồi chạy lại |
| `XA5207: Could not find android.jar for API level 36` | Script chọn nhầm Android SDK; chạy lệnh có `-AndroidSdkDirectory` trỏ tới SDK có API 36 |
| `running scripts is disabled on this system` | Chạy script bằng lệnh có `-ExecutionPolicy Bypass` thay cho Ctrl + Shift + B |

---

## 🔑 Tài khoản demo

Dùng sau khi nạp `sports_shop.sql`. Mật khẩu đều là `123456`.

| Nơi dùng | Tài khoản | Vai trò |
|---|---|---|
| Web Admin | `admin` | Quản lý |
| Web Admin | `nv_lan`, `nv_tuan`, `nv_hoa` | Nhân viên |
| Web bán hàng, app | `kh_an`, `kh_chi`, `kh_khanh` | Khách hạng Vàng |
| Web bán hàng, app | `kh_mai` | Khách hạng Bạc |
| Web bán hàng, app | `kh_dung` | Khách bị khóa, dùng để thử đăng nhập bị chặn |
