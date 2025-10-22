using System.Text.RegularExpressions;

namespace Scaffolding.NET.Extensions.MachineId;

internal class MacOSMachineIdProvider : IMachineIdProvider
{
    public string? GetBiosUuid()
    {
        // 通过 sysctl 或 ioreg 获取 IOPlatformUUID
        var outp = RunProcessAndCaptureStdout("sysctl", "-n kern.hostuuid", 1200);
        if (!string.IsNullOrWhiteSpace(outp)) return outp.Trim();

        outp = RunProcessAndCaptureStdout("ioreg", "-rd1 -c IOPlatformExpertDevice", 1200);
        if (string.IsNullOrWhiteSpace(outp)) return null;
        var m = Regex.Match(outp, "IOPlatformUUID\"\\s*=\\s*\"([^\"]+)\"");
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    public string? GetMotherboardSerial()
    {
        // macOS没有主板序列号，所以获取获取系统序列号
        var outp = RunProcessAndCaptureStdout("system_profiler", "SPHardwareDataType", 1200);
        if (string.IsNullOrWhiteSpace(outp)) return null;
        var m = Regex.Match(outp, "Serial Number \\(system\\):\\s*(.+)");
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    public string? GetFirstMac()
    {
        return MachineIdHelper.GetFirstPhysicalMacAddress();
    }

    public string? GetFirstDiskSerial()
    {
        var outp = RunProcessAndCaptureStdout("system_profiler", "SPStorageDataType", 1500);
        if (!string.IsNullOrWhiteSpace(outp))
        {
            // search for "Serial Number: XYZ"
            var m = Regex.Match(outp, "Serial Number:\\s*(.+)");
            if (m.Success) return m.Groups[1].Value.Trim();
        }

        // fallback
        var info = RunProcessAndCaptureStdout("diskutil", "info /dev/disk0", 800);
        if (!string.IsNullOrWhiteSpace(info))
        {
            var m = Regex.Match(info, "Device / Media Name:\\s*(.+)");
            if (m.Success) return m.Groups[1].Value.Trim();
        }

        return null;
    }

    private static string RunProcessAndCaptureStdout(string fileName, string args, int timeoutMs = 2000)
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
            if (proc.WaitForExit(timeoutMs)) return proc.StandardOutput.ReadToEnd();
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
        catch
        {
            return string.Empty;
        }
    }
}