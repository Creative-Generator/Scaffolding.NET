using System.Net;

namespace Scaffolding.NET;

public record ScaffoldingPlayerInfo
{
    public required bool IsHost { get; init; }
    public required IPAddress? Ip { get; init; }
    public required string HostName { get; init; }
}