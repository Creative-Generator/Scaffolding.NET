using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Scaffolding.NET.EasyTier;

public record EasyTierFileInfo
{
    /// <summary>
    /// 存放 EasyTier 的地址。
    /// Windows 上默认为 %localappdata%\easytier，macOS/Linux 上默认为 /usr/share/easytier。
    /// </summary>
    public string EasyTierFolderPath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "easytier");

    /// <summary>
    /// EasyTier 主程序的名字。
    /// Windows 上默认为 easytier-core.exe，macOS/Linux 上默认为 easytier-core。
    /// </summary>
    public string EasyTierCoreName { get; set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? "easytier-core.exe"
        : "easytier-core";

    /// <summary>
    /// EasyTier CLI 的名字。
    /// Windows 上默认为 easytier-cli.exe，macOS/Linux 上默认为 easytier-cli。
    /// </summary>
    public string EasyTierCliName { get; set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? "easytier-cli.exe"
        : "easytier-cli";

    /// <summary>
    /// EasyTier 的依赖 Packet 的名字。
    /// 只在 Windows 上有效，默认为 Packet.dll。
    /// </summary>
    public string EasyTierPacketLibraryName { get; set; } = "Packet.dll";
    
    /// <summary>
    /// 获取 EasyTier Core 的版本。
    /// </summary>
    /// <returns>返回 EasyTier Core 的版本。</returns>
    /// <exception cref="InvalidCastException">若未设置 EasyTier 地址与 EasyTier 主程序名，将会抛出该异常。</exception>
    public async Task<string> GetEasyTierVersionAsync(bool includeCommit = false)
    {
        if (!CheckEasyTierEnvironment()) throw new InvalidCastException("EasyTier 环境异常。");
        
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(EasyTierFolderPath, EasyTierCoreName),
            Arguments = "--version",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.EnableRaisingEvents = true;
            
#if NET6_0_OR_GREATER
#else
        var tcs = new TaskCompletionSource<object?>();
        process.Exited += (_, _) => tcs.TrySetResult(null);
#endif

        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        
#if NET6_0_OR_GREATER
        await process.WaitForExitAsync();
#else
        await tcs.Task;
#endif

        return includeCommit ? output.Split(' ')[1] : output.Split(' ')[1].Split('-')[0];
    }
    
    /// <summary>
    /// 检查 EasyTier 环境。
    /// </summary>
    /// <returns>如果环境正常，返回 true，否则返回 false。</returns>
    public bool CheckEasyTierEnvironment()
    {
        if (!(File.Exists(Path.Combine(EasyTierFolderPath, EasyTierCoreName)) &&
              File.Exists(Path.Combine(EasyTierFolderPath, EasyTierCliName))))
        {
            return (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
                    !File.Exists(Path.Combine(EasyTierFolderPath, EasyTierPacketLibraryName))) && false;
        }

        return true;
    }
}