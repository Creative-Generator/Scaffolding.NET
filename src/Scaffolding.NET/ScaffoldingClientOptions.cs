using Scaffolding.NET.EasyTier;

namespace Scaffolding.NET;

public record ScaffoldingClientOptions
{
    /// <summary>
    /// 房间码。
    /// </summary>
    public required string RoomId { get; set; }
    /// <summary>
    /// 机器码。
    /// </summary>
    public required string MachineId { get; set; }
    /// <summary>
    /// 玩家名。
    /// </summary>
    public required string PlayerName { get; set; }
    /// <summary>
    /// 软件标识。
    /// </summary>
    public string? Vendor { get; set; }
    /// <summary>
    /// 除标准协议额外支持的协议。
    /// </summary>
    public string[] AdvancedSupportedProtocols { get; set; } = [];
    /// <summary>
    /// EasyTier 文件信息。
    /// </summary>
    public required EasyTierFileInfo EasyTierFileInfo { get; set; }
    /// <summary>
    /// EasyTier 启动信息。
    /// </summary>
    public EasyTierStartInfo? EasyTierStartInfo { get; set; }
}