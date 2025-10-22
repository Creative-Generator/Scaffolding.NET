using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Scaffolding.NET.Extensions.MachineId;

internal class LinuxMachineIdProvider : IMachineIdProvider
{
    private static string ReadIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var t = File.ReadAllText(path).Trim();
                if (!string.IsNullOrEmpty(t))
                    return t;
            }
        }
        catch
        {
            // 忽略
        }

        return string.Empty;
    }

    public string? GetBiosUuid()
    {
        // product_uuid
        var v = ReadIfExists("/sys/class/dmi/id/product_uuid");
        if (!string.IsNullOrWhiteSpace(v) && v.Any(c => c != '0'))
            return v;
        // fallback
        v = ReadIfExists("/etc/machine-id");
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    public string? GetMotherboardSerial()
    {
        var v = ReadIfExists("/sys/class/dmi/id/board_serial");
        if (!string.IsNullOrWhiteSpace(v) && !v.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase))
            return v;
        return null;
    }

    public string? GetFirstMac()
    {
        return MachineIdHelper.GetFirstPhysicalMacAddress();
    }

    public string? GetFirstDiskSerial()
    {
        // 尝试常见的设备序列号文件：/sys/block/*/device/serial
        try
        {
            var blocksDir = "/sys/block";
            if (Directory.Exists(blocksDir))
            {
                var candidates = Directory.EnumerateDirectories(blocksDir)
                    .Where(d => !d.EndsWith("/loop") && !d.EndsWith("/ram"))
                    .Select(d =>
                    {
                        // 典型的序列路径
                        var serialPath = Path.Combine(d, "device", "serial");
                        return File.Exists(serialPath) ? ReadIfExists(serialPath) : string.Empty;
                    })
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                if (candidates.Any())
                    return candidates.First().Trim();
            }
        }
        catch
        {
            // 忽略
        }

        // fallback
        try
        {
            var outp = RunProcessAndCaptureStdout("lsblk", "-o NAME,SERIAL -P -n");
            if (!string.IsNullOrWhiteSpace(outp))
            {
                // 解析类似以下格式的行: NAME="sda" SERIAL="XYZ"
                var m = Regex.Match(outp, "NAME=\"sda\"\\s+SERIAL=\"([^\"]+)\"");
                if (m.Success) return m.Groups[1].Value;
                // 否则在输出中取第一个序列字段
                m = Regex.Match(outp, "SERIAL=\"([^\"]+)\"");
                if (m.Success) return m.Groups[1].Value;
            }
        }
        catch
        {
            // 忽略
        }

        return null;
    }

    private static string RunProcessAndCaptureStdout(string fileName, string args, int timeoutMs = 1500)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(fileName, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return string.Empty;
            if (!proc.WaitForExit(timeoutMs))
            {
                try
                {
                    proc.Kill();
                }
                catch
                {
                    // 忽略
                }

                return string.Empty;
            }

            return proc.StandardOutput.ReadToEnd();
        }
        catch
        {
            return string.Empty;
        }
    }
}