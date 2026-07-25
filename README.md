# MSI Center Mod

Trình quản lý **User Scenario không giới hạn** cho laptop MSI — thay thế/bổ sung cho tính năng User Scenario của MSI Center vốn chỉ cho 1 profile "User" duy nhất.

Tạo bao nhiêu scenario tùy thích (Gaming tối đa, Làm việc yên tĩnh, Pin tối đa, …), mỗi scenario gồm **mức hiệu năng + chế độ quạt (kèm đường cong 6 điểm) + Windows power mode**, áp bằng 1 click hoặc từ menu khay hệ thống.

Gồm 2 tab:
- **User Scenario** — quản lý và áp scenario.
- **Hardware Monitoring** — layout giống MSI Center gốc: donut CPU/GPU usage, Disk/Memory, bảng GPU (tên/clock/VRAM/nhiệt qua nvidia-smi), CPU, RAM, SSD, RPM 2 quạt (EC), LAN. Chỉ poll khi tab đang mở để không giữ dGPU thức gây tốn pin.

> Phát triển và kiểm chứng trên **MSI GP76 Leopard 11UG** (EC `17K3EMS1`), MSI Center 2.0.71, Windows 11.
> Về nguyên tắc chạy được trên mọi laptop MSI có WMI `MSI_ACPI` phiên bản ≥ 2 (đời ~2020 trở lên).

---

## Cách hoạt động

App **không** patch MSI Center. Nó nói chuyện trực tiếp với Embedded Controller (EC) qua đúng WMI provider mà MSI Center dùng — giao thức được dịch ngược từ `API_NB_Base Module.dll` / `MSIWMIACPI2.dll` của chính MSI Center:

| Thao tác | Cơ chế (`root\WMI` → `MSI_ACPI.InstanceName='ACPI\PNP0C14\0_0'`) |
|---|---|
| Mức hiệu năng (shift mode) | `Set_Data(0xD2)`: Turbo=`0xC4`, Cao=`0xC0`, Cân bằng=`0xC1`, Tiết kiệm=`0xC2` |
| Chế độ quạt | EC `0xD4`: bit 7 = Advanced, bit 4 = Silent; xóa cả hai = Auto |
| Đường cong quạt 6 điểm | `Get_Fan`/`Set_Fan(1=CPU, 2=GPU)`, tốc độ ở byte 1–6 (0–150%) |
| Cooler Boost | EC `0x98` bit 7 |
| Nhiệt độ / RPM hiện tại | `Get_Temperature(0)`, `Get_Fan(0)` |
| Power mode Windows | `PowerSetUserConfiguredAC/DCPowerMode` (GUID chuẩn) |

Gói WMI: 32 byte, byte 0 = sub-index/địa chỉ; gói trả về: byte 0 = cờ thành công, dữ liệu từ byte 1.

## Yêu cầu

- Windows 10/11, laptop MSI có MSI Center (hoặc tối thiểu driver WMI ACPI của MSI).
- .NET 10 Desktop Runtime.
- Quyền **Administrator** (WMI `MSI_ACPI` bắt buộc — app tự hiện UAC khi khởi động; từ chối thì chạy chế độ chỉ xem).

## Build & chạy

```bash
dotnet build MsiCenterMod.sln -c Release
```

Mở bằng Visual Studio 2026: mở `MsiCenterMod.sln`, F5. Chạy không cần UAC (dev/xem UI): thêm tham số `--no-elevate`.

File chạy: `src/MsiCenterMod/bin/Release/net10.0-windows10.0.17763.0/MsiCenterMod.exe`

Scenario lưu tại `C:\ProgramData\MSI Center Mod\scenarios.json`.

## Lưu ý quan trọng

- **Tắt AI Cooling / Smart Auto trong MSI Center** (Features → User Scenario) — nếu bật, MSI Center sẽ định kỳ ghi đè chế độ quạt của bạn. App có banner cảnh báo khi phát hiện.
- MSI Center vẫn có thể áp lại scenario của nó khi đổi nguồn AC/pin hoặc sau khi sleep — chỉ cần bấm áp lại scenario trong app (hoặc từ tray).
- Đường cong quạt bị chặn trong dải 0–150% giống MSI Center; EC vẫn giữ cơ chế tự bảo vệ nhiệt ở tầng firmware.

## Kiến trúc source

```
src/MsiCenterMod/
├── Models/                  # ScenarioProfile, FanCurve, HardwareStatus, các enum
├── Services/
│   ├── Abstractions/        # Toàn bộ interface (DI) — IMsiWmiClient, IHardwareController, IScenarioAspect, ...
│   ├── Hardware/
│   │   ├── MsiWmiClient.cs        # Giao thức WMI cấp thấp (gói 32 byte)
│   │   ├── MsiEcRegisters.cs      # Bản đồ thanh ghi EC + tên method (nguồn sự thật duy nhất)
│   │   ├── HardwareController.cs  # Facade: đọc trạng thái, chạy chuỗi aspect
│   │   └── Aspects/               # PerformanceAspect, FanAspect, PowerOverlayAspect
│   ├── Storage/             # JsonScenarioRepository (ghi atomic, seed mặc định)
│   └── System/              # ElevationService, PowerOverlayService, TrayIconService
├── ViewModels/              # MVVM (CommunityToolkit.Mvvm): Main / Scenario / FanPoint / Status
├── Views/                   # MainWindow (XAML)
├── Themes/DarkTheme.xaml    # Toàn bộ style tối
└── App.xaml(.cs)            # Bootstrap: UAC → single-instance → DI → UI
```

**Mở rộng tính năng mới** (TDP, giới hạn sạc pin, đèn phím, hotkey…): viết một class hiện thực `IScenarioAspect`, đăng ký một dòng trong `App.BuildServices` — không phải sửa code hiện có. Thêm trường tương ứng vào `ScenarioProfile` và UI editor nếu cần.

## Roadmap gợi ý

- [ ] TDP PL1/PL2 (EC `0x50`/`0x51`) cho các máy MSI hỗ trợ (có key registry `Manual\PL1`)
- [ ] Giới hạn % sạc pin (`Set_MasterBattery`)
- [ ] Hotkey toàn cục chuyển scenario
- [ ] Tự áp scenario theo app đang chạy / theo AC-DC
