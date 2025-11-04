using System.Net;

namespace Scaffolding.NET.EasyTier;

public record EasyTierStartInfo
{
    /// <summary>
    /// 是否尝试使用端口猜测打通对称性 NAT。
    /// </summary>
    public bool TryPunchSym { get; set; } = true;

    /// <summary>
    /// 是否启用 IPv6。
    /// </summary>
    public bool EnableIPv6 { get; set; } = true;

    /// <summary>
    /// 是否启用延迟优先模式。
    /// </summary>
    public bool LatencyFirstMode { get; set; } = true;

    /// <summary>
    /// 决定 EasyTier 默认使用的协议。
    /// </summary>
    public EasyTierDefaultProtocol DefaultProtocol { get; set; } = EasyTierDefaultProtocol.Tcp;
    
    /// <summary>
    /// 网络名称。
    /// </summary>
    internal string? NetworkName { get; set; }
    
    /// <summary>
    /// 网络密钥。
    /// </summary>
    internal string? NetworkSecret { get; set; }
    
    /// <summary>
    /// 机器码。
    /// </summary>
    public string? MachineId { get; set; }
    
    /// <summary>
    /// TCP 端口白名单。
    /// </summary>
    internal string[]? TcpWhiteList { get; set; }
    
    /// <summary>
    /// UDP 端口白名单。
    /// </summary>
    internal string[]? UdpWhiteList { get; set; }

    /// <summary>
    /// 用于标识此设备的主机名。
    /// </summary>
    internal string HostName { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 此节点的 IPv4 地址。注意：该选项会覆盖“是否启用 DHCP”。
    /// </summary>
    public IPAddress? Ipv4Address { get; set; }
    
    /// <summary>
    /// 是否启用 DHCP。注意：该选项会被“此节点的 IPv4 地址”覆盖。
    /// </summary>
    public bool Dhcp { get; set; } = true;

    /// <summary>
    /// 是否允许为互联网上的其他用户中继流量。
    /// </summary>
    public bool RelayForOthers { get; set; } = true;

    /// <summary>
    /// 是否禁用 P2P。
    /// </summary>
    public bool DisableP2P { get; set; } = false;
    
    /// <summary>
    /// 除官方节点和社区节点外的中继服务器。
    /// </summary>
    public string[]? RelayServers { get; set; }
}