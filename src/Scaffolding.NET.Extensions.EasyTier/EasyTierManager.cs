using System;
using System.IO;
using System.Runtime.InteropServices;
using Scaffolding.NET.EasyTier;

namespace Scaffolding.NET.Extensions.EasyTier;

public static class EasyTierManager
{
    public static EasyTierFileInfo? FileInfo { get; set; } = new();

    

    /// <summary>
    /// 获取最新版本的 EasyTier 下载链接。
    /// </summary>
    /// <returns>EasyTier 下载链接。</returns>
    public static string GetLatestEasyTierDownloadUrl()
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => "i686",
            Architecture.X64 => "x86_64",
            Architecture.Arm when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => "arm",
            Architecture.Arm64 => "aarch64",
#if NET8_0_OR_GREATER
            Architecture.LoongArch64 when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => "loongarch64",
#endif
#if NET9_0_OR_GREATER
            Architecture.RiscV64 when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => "riscv64",
#endif
            _ => throw new NotSupportedException("不支持的架构。")
        };

        string os;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            os = "windows";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            os = "linux";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            os = "macos";
        }
#if NET6_0_OR_GREATER
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
        {
            if (arch != "x86_64") throw new NotSupportedException("不支持的架构。");
            os = "freebsd";
        }
#endif
        else
        {
            throw new NotSupportedException("不支持的操作系统。");
        }

        throw new NotImplementedException();
    }
}