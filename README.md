# MSI Center Mod

An **unlimited User Scenario manager** for MSI laptops — a lightweight replacement for MSI Center's User Scenario feature, which only allows a single custom "User" profile.

Create as many scenarios as you like (Max Gaming, Quiet Work, Battery Saver, …). Each scenario bundles **performance level + fan mode (with a 6-point fan curve) + Windows power mode**, applied with one click or straight from the system tray.

> Developed and verified on an **MSI GP76 Leopard 11UG** (EC `17K3EMS1`), MSI Center 2.0.71, Windows 11.
> Should work on any MSI laptop exposing the `MSI_ACPI` WMI interface protocol version ≥ 2 (roughly 2020 and newer).

## Features

- **User Scenario** tab — create/edit/apply unlimited scenarios:
  - Performance level: Turbo / High / Balanced / Eco (EC shift mode)
  - Fan: Auto / Silent / Advanced 6-point CPU & GPU curves / Cooler Boost
  - Optional Windows power mode overlay per scenario
- **Hardware Monitoring** tab — MSI Center-style dashboard: CPU/GPU usage donuts, disk & memory, GPU name/clock/VRAM/temperature (nvidia-smi), CPU temperature and both fan RPMs (EC), RAM, SSD, LAN. Polls only while the tab is open so the dGPU is not kept awake.
- **System Diagnosis** tab:
  - **Battery Master** — charge limit modes (100% / 80% / 60%) via EC register `0xD7`, plus a Battery Calibration launcher when MSI's tool is installed
  - **System Checker** — disk/memory load with Clean Up Disk (cleanmgr) and Free Up Memory (working-set trim)
  - **SSD Status** — per-physical-disk usage donut, capacity breakdown, temperature and health (Windows Storage WMI)
- **Start with Windows** — Task Scheduler task with highest run level (no UAC prompt at logon), starts hidden in the tray
- **Auto-reapply last scenario** — on app start, after sleep/resume, and on AC ↔ battery switch (the EC resets to defaults on every boot; this keeps your scenario sticky without MSI Center)
- **Bilingual UI** — Vietnamese / English, switchable at runtime with no restart (WPF DynamicResource dictionaries)
- Tray icon: apply any scenario from the context menu (the last-applied one is check-marked); closing the window minimizes to tray

## How it works

The app does **not** patch MSI Center. It talks directly to the Embedded Controller through the same WMI provider MSI Center uses — the protocol was reverse-engineered from MSI Center's own binaries (`API_NB_Base Module.dll`, `API_NB_System Diagnosis.dll`, `MSIWMIACPI2.dll`):

| Operation | Mechanism (`root\WMI` → `MSI_ACPI.InstanceName='ACPI\PNP0C14\0_0'`) |
|---|---|
| Performance level (shift mode) | `Set_Data(0xD2)`: Turbo=`0xC4`, High=`0xC0`, Balanced=`0xC1`, Eco=`0xC2` |
| Fan mode | EC `0xD4`: bit 7 = Advanced, bit 4 = Silent; both cleared = Auto |
| 6-point fan curves | `Get_Fan`/`Set_Fan(1=CPU, 2=GPU)`, speeds in bytes 1–6 (0–150%) |
| Cooler Boost | EC `0x98` bit 7 |
| Battery charge limit | EC `0xD7`: bit 7 preserved, low 7 bits = stop percentage (100/80/60) |
| Current temps / fan RPM | `Get_Temperature(0)`, `Get_Fan(0)` |
| Windows power overlay | `PowerSetUserConfiguredAC/DCPowerMode` (standard overlay GUIDs) |

WMI packet layout: 32 bytes, byte 0 = sub-index/EC address; responses: byte 0 = success flag, data from byte 1. Input parameters must be cloned from `GetMethodParameters("Set_Data")` with a fallback to `Get_WMI` output — exactly what MSI Center does internally.

## Requirements

- Windows 10/11 on an MSI laptop (MSI's WMI ACPI firmware interface must be present)
- .NET 10 Desktop Runtime
- **Administrator** rights (required by the `MSI_ACPI` WMI provider; the app self-elevates via UAC — declining leaves it in read-only mode)

## Build & run

```bash
dotnet build MsiCenterMod.sln -c Release
```

Open `MsiCenterMod.sln` in Visual Studio 2026 and press F5, or run the published binary:

`src/MsiCenterMod/bin/Release/net10.0-windows10.0.17763.0/MsiCenterMod.exe`

Dev switches: `--no-elevate` (skip UAC, read-only, allows a second instance), `--tab=N` (open tab N), `--autostart` (start hidden in tray — used by the scheduled task).

Data files: `C:\ProgramData\MSI Center Mod\scenarios.json`, `settings.json`, `logs\app.log`.

## Important notes

- **Disable AI Cooling / Smart Auto in MSI Center** (Features → User Scenario) — otherwise MSI Center periodically overrides fan settings. The app shows a warning banner when it detects this.
- If MSI Center is still installed, its service may re-apply its own scenario after resume or power-source changes; with **auto-reapply** enabled this app immediately re-applies yours. You can also stop/disable `MSI_Center_Service` entirely — this app does not depend on it.
- Fan speeds are clamped to 0–150% (same range as MSI Center Advanced mode); the EC's firmware-level thermal failsafes remain active regardless.

## Architecture

```
src/MsiCenterMod/
├── Models/                  # ScenarioProfile, FanCurve, HardwareStatus, AppSettings, enums
├── Services/
│   ├── Abstractions/        # All DI interfaces (IMsiWmiClient, IHardwareController, IScenarioAspect, ...)
│   ├── Hardware/
│   │   ├── MsiWmiClient.cs        # Low-level WMI protocol (32-byte packets)
│   │   ├── MsiEcRegisters.cs      # EC register map + method names (single source of truth)
│   │   ├── HardwareController.cs  # Facade: status reads, runs the aspect pipeline
│   │   ├── BatteryChargeService.cs
│   │   └── Aspects/               # PerformanceAspect, FanAspect, PowerOverlayAspect
│   ├── Storage/             # JSON repositories (scenarios, settings) with atomic writes
│   └── System/              # Elevation, power overlay, tray icon, autostart task,
│                            # auto-reapply, system metrics, storage info, memory cleaner, logger
├── ViewModels/              # MVVM (CommunityToolkit.Mvvm): Main / Scenario / Monitoring / Diagnosis
├── Views/                   # MainWindow + MonitoringView + DiagnosisView
├── Controls/                # DonutGauge (custom-drawn percentage ring)
├── Themes/DarkTheme.xaml    # Complete dark theme
└── App.xaml(.cs)            # Bootstrap: UAC → single instance → DI → UI
```

**Adding a new per-scenario feature** (TDP, keyboard backlight, hotkeys, …): implement `IScenarioAspect`, register one line in `App.BuildServices` — no existing code changes needed (Open/Closed principle). Add the corresponding field to `ScenarioProfile` and editor UI if required.

## Roadmap

- [ ] TDP PL1/PL2 (EC `0x50`/`0x51`) for MSI models that support it (registry key `Manual\PL1` present)
- [ ] Global hotkeys for scenario switching
- [ ] Per-app automatic scenario switching
- [x] Battery charge limit (v1.2.0)
- [x] Start with Windows + auto-reapply (v1.2.0)

## Disclaimer

Not affiliated with MSI. EC writes use the exact same values MSI Center writes, but you use this software at your own risk.
