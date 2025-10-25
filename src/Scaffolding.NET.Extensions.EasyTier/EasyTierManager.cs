using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Scaffolding.NET.Extensions.EasyTier;

public static class EasyTierManager
{
    /// <summary>
    /// 存放 EasyTier 的地址。
    /// Windows 上默认为 %localappdata%\easytier，macOS/Linux 上默认为 /usr/share/easytier。
    /// </summary>
    public static string EasyTierFolderPath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "easytier");

    /// <summary>
    /// EasyTier 主程序的名字。
    /// Windows 上默认为 easytier-core.exe，macOS/Linux 上默认为 easytier-core。
    /// </summary>
    public static string EasyTierCoreName { get; set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? "easytier-core.exe"
        : "easytier-core";

    /// <summary>
    /// EasyTier CLI 的名字。
    /// Windows 上默认为 easytier-cli.exe，macOS/Linux 上默认为 easytier-cli。
    /// </summary>
    public static string EasyTierCliName { get; set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? "easytier-cli.exe"
        : "easytier-cli";

    /// <summary>
    /// EasyTier 的依赖 Packet 的名字。
    /// 只在 Windows 上有效，默认为 Packet.dll。
    /// </summary>
    public static string EasyTierPacketLibraryName { get; set; } = "Packet.dll";

    /// <summary>
    /// 检查 EasyTier 环境。
    /// </summary>
    /// <returns>如果环境正常，返回 true，否则返回 false。</returns>
    public static bool CheckEasyTierEnvironment()
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