using System.Net;
using Scaffolding.NET.EasyTier;

namespace Scaffolding.NET.Helpers;

internal static class PlayerInfoHelper
{
    internal static ScaffoldingPlayerInfo ConvertEasyTierPeerInfoToScaffoldingPlayerInfo(EasyTierPeerInfo peerInfo)
    {
        return new ScaffoldingPlayerInfo
        {
            IsHost = peerInfo.Hostname.StartsWith("scaffolding-mc-server", StringComparison.Ordinal),
            Ip = IPAddress.Parse(peerInfo.Ipv4),
            HostName = peerInfo.Hostname
        };
    }
}