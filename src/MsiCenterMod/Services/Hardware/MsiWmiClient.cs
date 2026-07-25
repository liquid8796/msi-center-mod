using System.Management;
using System.Text;
using MsiCenterMod.Services.Abstractions;

namespace MsiCenterMod.Services.Hardware;

/// <summary>
/// Hiện thực <see cref="IMsiWmiClient"/> theo đúng giao thức của MSIWMIACPI2.dll:
///  - ManagementObject: root\WMI, MSI_ACPI.InstanceName='ACPI\PNP0C14\0_0'.
///  - Tham số vào/ra là object nhúng "Data" có thuộc tính "Bytes" (byte[32]).
///  - Byte 0 của gói trả về là cờ thành công; dữ liệu thật bắt đầu từ byte 1.
/// Mọi lời gọi được khóa (lock) vì WMI instance không thread-safe.
/// </summary>
public sealed class MsiWmiClient : IMsiWmiClient
{
    private const string Scope = @"root\WMI";
    private const string Path = @"MSI_ACPI.InstanceName='ACPI\PNP0C14\0_0'";
    private const int PackageSize = 32;

    private readonly Lock _sync = new();
    private ManagementObject? _instance;

    public bool IsAvailable { get; private set; }

    public int WmiMajorVersion { get; private set; }

    public string EcFirmwareInfo { get; private set; } = string.Empty;

    public bool Initialize(out string error)
    {
        lock (_sync)
        {
            try
            {
                _instance = new ManagementObject(Scope, Path, null);
                _instance.Get(); // ép bind ngay để lỗi quyền/không tồn tại lộ ra tại đây

                // Get_WMI (không tham số): raw[1] = major, raw[2] = minor
                byte[]? wmiInfo = InvokeRaw(MsiEcRegisters.GetWmi, null);
                if (wmiInfo is { Length: > 2 })
                {
                    WmiMajorVersion = wmiInfo[1];
                }

                // Get_EC (không tham số): raw[2..29] = chuỗi phiên bản firmware EC
                byte[]? ecInfo = InvokeRaw(MsiEcRegisters.GetEc, null);
                if (ecInfo is { Length: >= 30 })
                {
                    EcFirmwareInfo = Encoding.UTF8
                        .GetString(ecInfo, 2, 28)
                        .Trim('\0', ' ');
                }

                if (WmiMajorVersion < 2)
                {
                    error = $"Phiên bản WMI {WmiMajorVersion} không được hỗ trợ (cần ≥ 2 — máy MSI đời 2020 trở lên).";
                    IsAvailable = false;
                    return false;
                }

                IsAvailable = true;
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                IsAvailable = false;
                error = ex is ManagementException me
                    ? $"Không truy cập được MSI_ACPI WMI ({me.ErrorCode}). Hãy chạy bằng quyền Administrator trên máy MSI."
                    : ex.Message;
                return false;
            }
        }
    }

    public bool TryRead(string method, byte subIndex, out byte[] data)
    {
        var package = new byte[PackageSize];
        package[0] = subIndex;

        byte[]? raw;
        lock (_sync)
        {
            raw = InvokeRaw(method, package);
        }

        if (raw is null || raw.Length == 0 || raw[0] == 0)
        {
            data = [];
            return false;
        }

        // Bỏ byte cờ — giữ nguyên cách đánh chỉ số Data[i] của MSI Center.
        data = new byte[raw.Length - 1];
        Array.Copy(raw, 1, data, 0, data.Length);
        return true;
    }

    public bool TryWrite(string method, byte subIndex, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length >= PackageSize)
        {
            throw new ArgumentException($"Payload tối đa {PackageSize - 1} byte.", nameof(payload));
        }

        var package = new byte[PackageSize];
        package[0] = subIndex;
        Array.Copy(payload, 0, package, 1, payload.Length);

        byte[]? raw;
        lock (_sync)
        {
            raw = InvokeRaw(method, package);
        }

        return raw is { Length: > 0 } && raw[0] > 0;
    }

    /// <summary>Gọi method WMI thô; <paramref name="package"/> = null cho method không tham số.</summary>
    private byte[]? InvokeRaw(string method, byte[]? package)
    {
        if (_instance is null)
        {
            return null;
        }

        ManagementBaseObject? outParams;
        if (package is null)
        {
            outParams = _instance.InvokeMethod(method, null, null);
        }
        else
        {
            using ManagementBaseObject inParams = _instance.GetMethodParameters(method);
            var dataObject = (ManagementBaseObject)inParams["Data"];
            dataObject.SetPropertyValue("Bytes", package);
            inParams.SetPropertyValue("Data", dataObject);
            outParams = _instance.InvokeMethod(method, inParams, null);
        }

        if (outParams is null)
        {
            return null;
        }

        using (outParams)
        {
            return (outParams["Data"] as ManagementBaseObject)?["Bytes"] as byte[];
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _instance?.Dispose();
            _instance = null;
        }
    }
}
