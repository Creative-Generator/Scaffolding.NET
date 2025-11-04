using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Scaffolding.NET.Extensions.MachineId;

public static class MachineIdHelper
{
    private static IMachineIdProvider? _machineIdProvider;
    
    public static async Task<string> GetMachineIdAsync()
    {
        return await Task.Run(GetMachineId);
    }
    
    public static string GetMachineId()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _machineIdProvider ??= new WindowsMachineIdProvider();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            _machineIdProvider ??= new LinuxMachineIdProvider();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            _machineIdProvider ??= new MacOSMachineIdProvider();
        }
        else
        {
            throw new NotSupportedException("不支持的操作系统");
        }

        var biosUuid = _machineIdProvider.GetBiosUuid();
        var motherboardSerial = _machineIdProvider.GetMotherboardSerial();
        var firstMac = _machineIdProvider.GetFirstMac();
        var firstDiskSerial = _machineIdProvider.GetFirstDiskSerial();

        var rawMachineId = biosUuid + motherboardSerial + firstMac + firstDiskSerial;
        
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(rawMachineId));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
    
    internal static string? GetFirstPhysicalMacAddress()
    {
        try
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n =>
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    n.OperationalStatus == OperationalStatus.Up &&
                    n.GetPhysicalAddress().GetAddressBytes().Length == 6)
                .OrderBy(n => n.Name)
                .ToArray();

            foreach (var nic in nics)
            {
                var mac = nic.GetPhysicalAddress().ToString();
                if (string.IsNullOrWhiteSpace(mac)) continue;
                
                // 过滤零值
                if (mac.Any(c => c != '0'))
                {
                    return NormalizeHex(mac!);
                }
            }
        }
        catch
        {
            // 忽略
        }
        return null;
    }
    
    private static string NormalizeHex(string raw)
    {
        // 移除分隔符和大小写
        var sb = new StringBuilder();
        foreach (var ch in raw.Where(Uri.IsHexDigit))
        {
            sb.Append(char.ToUpperInvariant(ch));
        }
        return sb.ToString();
    }
}