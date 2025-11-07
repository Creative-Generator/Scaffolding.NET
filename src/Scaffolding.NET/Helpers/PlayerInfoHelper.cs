using System.Net;
using Scaffolding.NET.EasyTier;

namespace Scaffolding.NET.Helpers;

internal static class PlayerInfoHelper
{
    internal static ScaffoldingPlayerInfo ConvertEasyTierPeerInfoToScaffoldingPlayerInfo(EasyTierPeerInfo peerInfo)
    {
        var ip = string.IsNullOrEmpty(peerInfo.Ipv4) ? null : IPAddress.Parse(peerInfo.Ipv4);
        
        return new ScaffoldingPlayerInfo
        {
            IsHost = peerInfo.Hostname.StartsWith("scaffolding-mc-server", StringComparison.Ordinal),
            Ip = ip,
            HostName = peerInfo.Hostname
        };
    }
}