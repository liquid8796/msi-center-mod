using System.Management;
using System.Text;
using MsiCenterMod.Services.Abstractions;
using MsiCenterMod.Services.System;

namespace MsiCenterMod.Services.Hardware;

/// <summary>
/// Hiện thực <see cref="IMsiWmiClient"/> theo đúng giao thức của MSIWMIACPI2.dll:
///  - ManagementObject: root\WMI, MSI_ACPI.InstanceName='ACPI\PNP0C14\0_0'.
///  - QUAN TRỌNG: bộ tham số vào được clone từ GetMethodParameters("Set_Data")
///    và dùng chung cho MỌI method (kỹ thuật của chính MSI Center) — mỗi method
///    tự khai báo kích thước buffer khác nhau nên không thể lấy theo từng method.
///  - Gói 32 byte: byte[0] = sub-index/địa chỉ; gói trả về: byte[0] = cờ thành công.
/// Mọi lời gọi được khóa (lock) vì WMI instance không thread-safe, và mọi exception
/// đều được bắt tại đây — tầng trên chỉ nhận true/false.
/// </summary>
public sealed class MsiWmiClient : IMsiWmiClient
{
    private const string Scope = @"root\WMI";
    private const string Path = @"MSI_ACPI.InstanceName='ACPI\PNP0C14\0_0'";
    private const int PackageSize = 32;

    private readonly Lock _sync = new();
    private ManagementObject? _instance;
    private ManagementBaseObject? _inParamsTemplate;

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

                CreateInParamsTemplate();

                // Get_WMI (không tham số): raw[1] = major, raw[2] = minor
                byte[]? wmiInfo = InvokeRawUnsafe(MsiEcRegisters.GetWmi, null);
                if (wmiInfo is { Length: > 2 })
                {
                    WmiMajorVersion = wmiInfo[1];
                }

                // Get_EC (không tham số): raw[2..29] = chuỗi phiên bản firmware EC
                byte[]? ecInfo = InvokeRawUnsafe(MsiEcRegisters.GetEc, null);
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
                AppLogger.Info($"WMI sẵn sàng: version={WmiMajorVersion}, EC={EcFirmwareInfo}");
                return true;
            }
            catch (Exception ex)
            {
                IsAvailable = false;
                error = ex is ManagementException me
                    ? $"Không truy cập được MSI_ACPI WMI ({me.ErrorCode}). Hãy chạy bằng quyền Administrator trên máy MSI."
                    : ex.Message;
                AppLogger.Error("Initialize WMI thất bại", ex);
                return false;
            }
        }
    }

    /// <summary>
    /// Tạo template tham số vào — giống hệt CreateParamsInstance của MSI Center:
    /// thử GetMethodParameters("Set_Data") trước; nếu thất bại HOẶC thuộc tính Data
    /// bên trong rỗng (xảy ra trên nhiều máy — chính vì vậy MSI Center mới có fallback)
    /// thì dùng luôn kết quả trả về của Get_WMI, vốn luôn chứa object Data thật.
    /// </summary>
    private void CreateInParamsTemplate()
    {
        try
        {
            ManagementBaseObject? fromDefinition = _instance!.GetMethodParameters(MsiEcRegisters.SetData);
            if (fromDefinition?["Data"] is ManagementBaseObject)
            {
                _inParamsTemplate = fromDefinition;
                return;
            }

            fromDefinition?.Dispose();
            AppLogger.Info("GetMethodParameters(Set_Data) có Data rỗng — fallback sang Get_WMI.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("GetMethodParameters(Set_Data) thất bại — fallback sang Get_WMI", ex);
        }

        ManagementBaseObject? fromGetWmi = _instance!.InvokeMethod(MsiEcRegisters.GetWmi, null, null);
        if (fromGetWmi?["Data"] is ManagementBaseObject)
        {
            _inParamsTemplate = fromGetWmi;
            return;
        }

        fromGetWmi?.Dispose();
        throw new InvalidOperationException(
            "Không tạo được bộ tham số WMI (cả Set_Data lẫn Get_WMI đều không có thuộc tính Data).");
    }

    public bool TryRead(string method, byte subIndex, out byte[] data)
    {
        var package = new byte[PackageSize];
        package[0] = subIndex;

        byte[]? raw;
        lock (_sync)
        {
            try
            {
                raw = InvokeRawUnsafe(method, package);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"TryRead {method}({subIndex}) thất bại", ex);
                data = [];
                return false;
            }
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
            try
            {
                raw = InvokeRawUnsafe(method, package);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"TryWrite {method}(0x{subIndex:X2}) thất bại", ex);
                return false;
            }
        }

        bool ok = raw is { Length: > 0 } && raw[0] > 0;
        if (!ok)
        {
            AppLogger.Error($"TryWrite {method}(0x{subIndex:X2}) bị EC từ chối (flag=0).");
        }

        return ok;
    }

    /// <summary>
    /// Gọi method WMI thô; <paramref name="package"/> = null cho method không tham số.
    /// Phải gọi trong lock. Có thể ném ManagementException — caller tự bắt.
    /// </summary>
    private byte[]? InvokeRawUnsafe(string method, byte[]? package)
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
            // Clone template Set_Data cho mọi method — kỹ thuật của MSI Center.
            var inParams = (ManagementBaseObject)_inParamsTemplate!.Clone();
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
            _inParamsTemplate?.Dispose();
            _inParamsTemplate = null;
            _instance?.Dispose();
            _instance = null;
        }
    }
}
