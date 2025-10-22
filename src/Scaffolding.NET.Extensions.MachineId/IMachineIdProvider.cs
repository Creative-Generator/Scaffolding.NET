namespace Scaffolding.NET.Extensions.MachineId;

internal interface IMachineIdProvider
{
    /// <summary>BIOS / SMBIOS UUID (product_uuid)</summary>
    string? GetBiosUuid();

    /// <summary>主板序列号</summary>
    string? GetMotherboardSerial();

    /// <summary>第一个“物理”MAC 地址（去掉虚拟/loopback）</summary>
    string? GetFirstMac();

    /// <summary>第一个物理磁盘的序列号</summary>
    string? GetFirstDiskSerial();
}