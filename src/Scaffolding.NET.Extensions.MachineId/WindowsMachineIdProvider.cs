using System;
using System.Linq;
using System.Management;
#if NET6_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace Scaffolding.NET.Extensions.MachineId;

#if NET6_0_OR_GREATER
[SupportedOSPlatform("windows")]
#endif
internal sealed class WindowsMachineIdProvider : IMachineIdProvider
{
    public string? GetBiosUuid()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct");
            foreach (var mo in searcher.Get().Cast<ManagementObject>())
            {
                var v = mo["UUID"]?.ToString();
                if (!string.IsNullOrWhiteSpace(v) && !v!.Equals("00000000-0000-0000-0000-000000000000"))
                    return v.Trim();
            }
        }
        catch
        {
            // 忽略
        }

        return null;
    }

    public string? GetMotherboardSerial()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
            foreach (var mo in searcher.Get().Cast<ManagementObject>())
            {
                var v = mo["SerialNumber"]?.ToString();
                if (!string.IsNullOrWhiteSpace(v) &&
                    !v!.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase))
                    return v.Trim();
            }
        }
        catch
        {
            // 忽略
        }

        return null;
    }

    public string? GetFirstMac()
    {
        // re-use helper
        return MachineIdHelper.GetFirstPhysicalMacAddress();
    }

    public string? GetFirstDiskSerial()
    {
        try
        {
            // Win32_PhysicalMedia
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_PhysicalMedia");
            foreach (var mo in searcher.Get().Cast<ManagementObject>())
            {
                var v = mo["SerialNumber"]?.ToString();
                if (!string.IsNullOrWhiteSpace(v))
                    return v?.Trim();
            }

            // Win32_DiskDrive (fallback)
            using var searcher2 = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive");
            foreach (var mo in searcher2.Get().Cast<ManagementObject>())
            {
                var v = mo["SerialNumber"]?.ToString();
                if (!string.IsNullOrWhiteSpace(v))
                    return v?.Trim();
            }
        }
        catch
        {
            // 忽略
        }

        return null;
    }
}