namespace MsiCenterMod.Services.Abstractions;

/// <summary>
/// Client cấp thấp nói chuyện với WMI provider của MSI
/// (root\WMI → MSI_ACPI.InstanceName='ACPI\PNP0C14\0_0').
///
/// Giao thức (dịch ngược từ MSIWMIACPI2.dll của MSI Center):
///  - Mọi method nhận gói 32 byte: byte[0] = sub-index/địa chỉ, byte[1..] = payload.
///  - Gói trả về 32 byte: byte[0] = cờ thành công (&gt;0), byte[1..] = dữ liệu.
/// </summary>
public interface IMsiWmiClient : IDisposable
{
    /// <summary>WMI đã sẵn sàng và đúng phiên bản giao thức (major ≥ 2).</summary>
    bool IsAvailable { get; }

    int WmiMajorVersion { get; }

    /// <summary>Chuỗi phiên bản firmware EC, ví dụ "17K3EMS1.115...".</summary>
    string EcFirmwareInfo { get; }

    /// <summary>Kết nối và đọc phiên bản WMI/EC. Trả về thông báo lỗi nếu thất bại.</summary>
    bool Initialize(out string error);

    /// <summary>
    /// Gọi method Get_* với sub-index; trả về dữ liệu đã bỏ byte cờ
    /// (data[i] ở đây tương ứng InvokeResults.Data[i] trong code MSI Center).
    /// </summary>
    bool TryRead(string method, byte subIndex, out byte[] data);

    /// <summary>Gọi method Set_* : gói ghi là [subIndex, payload...].</summary>
    bool TryWrite(string method, byte subIndex, byte[] payload);
}
