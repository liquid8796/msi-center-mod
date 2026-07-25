using MsiCenterMod.Models;
using MsiCenterMod.Services.Abstractions;

namespace MsiCenterMod.Services.Hardware;

/// <summary>
/// Facade phần cứng: khởi tạo WMI một lần, đọc trạng thái/đường cong quạt,
/// và áp scenario bằng cách chạy tuần tự các <see cref="IScenarioAspect"/> theo Order.
/// Mọi thao tác WMI chạy trên thread pool để không chặn UI.
/// </summary>
public sealed class HardwareController : IHardwareController
{
    private readonly IMsiWmiClient _wmi;
    private readonly IReadOnlyList<IScenarioAspect> _aspects;

    public bool IsOperational { get; }

    public string? UnavailableReason { get; }

    public string? UnavailableReasonKey { get; }

    public string EcFirmwareInfo => _wmi.EcFirmwareInfo;

    public HardwareController(
        IMsiWmiClient wmi,
        IEnumerable<IScenarioAspect> aspects,
        IElevationService elevation)
    {
        _wmi = wmi;
        _aspects = aspects.OrderBy(a => a.Order).ToList();

        if (!elevation.IsElevated)
        {
            IsOperational = false;
            UnavailableReason = "Chưa có quyền Administrator — chỉ xem, không điều khiển được phần cứng.";
            UnavailableReasonKey = "S.Hw.NoAdmin";
            return;
        }

        IsOperational = _wmi.Initialize(out string error);
        UnavailableReason = IsOperational ? null : error;
        UnavailableReasonKey = null; // lỗi WMI là chuỗi kỹ thuật động — hiển thị nguyên văn
    }

    public Task<HardwareStatus?> ReadStatusAsync(CancellationToken ct = default)
    {
        if (!IsOperational)
        {
            return Task.FromResult<HardwareStatus?>(null);
        }

        return Task.Run<HardwareStatus?>(() =>
        {
            // Từng nhóm đọc độc lập — nhóm nào lỗi thì để giá trị 0, không phá cả status.
            byte shift = 0, fanFlags = 0;
            bool coolerBoost = false;
            int cpuTemp = 0, gpuTemp = 0, cpuRpm = 0, gpuRpm = 0;

            if (_wmi.TryRead(MsiEcRegisters.GetAp, MsiEcRegisters.ApShiftGroup, out byte[] ap0) && ap0.Length > 2)
            {
                shift = ap0[2];
            }

            if (_wmi.TryReadFanFlags(out byte flags))
            {
                fanFlags = flags;
            }

            if (_wmi.TryReadCoolerBoost(out byte boost))
            {
                coolerBoost = (boost & MsiEcRegisters.CoolerBoostBit) != 0;
            }

            if (_wmi.TryRead(MsiEcRegisters.GetTemperature, MsiEcRegisters.TemperatureCurrentGroup, out byte[] temps)
                && temps.Length > 1)
            {
                cpuTemp = temps[0];
                gpuTemp = temps[1];
            }

            if (_wmi.TryRead(MsiEcRegisters.GetFan, MsiEcRegisters.FanRpmGroup, out byte[] rpm) && rpm.Length > 3)
            {
                cpuRpm = MsiEcRegisters.ToRpm(rpm[0], rpm[1]);
                gpuRpm = MsiEcRegisters.ToRpm(rpm[2], rpm[3]);
            }

            return new HardwareStatus
            {
                ShiftModeRaw = shift,
                FanModeRaw = fanFlags,
                IsCoolerBoostOn = coolerBoost,
                CpuTemperature = cpuTemp,
                GpuTemperature = gpuTemp,
                CpuFanRpm = cpuRpm,
                GpuFanRpm = gpuRpm,
            };
        }, ct);
    }

    public Task<FanCurve?> ReadFanCurveAsync(FanTarget target, CancellationToken ct = default)
    {
        if (!IsOperational)
        {
            return Task.FromResult<FanCurve?>(null);
        }

        return Task.Run<FanCurve?>(() =>
        {
            byte fanGroup = target == FanTarget.Cpu ? MsiEcRegisters.FanCurveCpu : MsiEcRegisters.FanCurveGpu;
            byte tempGroup = target == FanTarget.Cpu
                ? MsiEcRegisters.TemperatureCurveCpu
                : MsiEcRegisters.TemperatureCurveGpu;

            if (!_wmi.TryRead(MsiEcRegisters.GetFan, fanGroup, out byte[] fan) || fan.Length < 7)
            {
                return null;
            }

            var speeds = new int[FanCurve.PointCount];
            var temperatures = new int[FanCurve.PointCount];
            for (int i = 0; i < FanCurve.PointCount; i++)
            {
                speeds[i] = fan[i + 1];
            }

            if (_wmi.TryRead(MsiEcRegisters.GetTemperature, tempGroup, out byte[] temp) && temp.Length >= 7)
            {
                for (int i = 0; i < FanCurve.PointCount; i++)
                {
                    temperatures[i] = temp[i + 1];
                }
            }

            return new FanCurve(speeds, temperatures);
        }, ct);
    }

    public async Task<ScenarioApplyResult> ApplyScenarioAsync(ScenarioProfile profile, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!IsOperational)
        {
            return new ScenarioApplyResult(false, [UnavailableReason ?? "Phần cứng chưa sẵn sàng."]);
        }

        var errors = new List<string>();
        foreach (IScenarioAspect aspect in _aspects)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await Task.Run(() => aspect.ApplyAsync(profile, ct), ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                errors.Add($"{aspect.DisplayName}: {ex.Message}");
            }
        }

        return errors.Count == 0 ? ScenarioApplyResult.Ok : new ScenarioApplyResult(false, errors);
    }
}
