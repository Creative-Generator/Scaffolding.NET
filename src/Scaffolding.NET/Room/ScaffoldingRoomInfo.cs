namespace Scaffolding.NET.Room;

public record ScaffoldingRoomInfo
{
    public IReadOnlyList<ScaffoldingPlayerInfo> PlayerInfos { get; internal set; } = null!;
    public ushort Port { get; internal set; }
    public int Latency { get; internal set; }
    public IReadOnlyList<string> SupportedRequest { get; internal set; } =
        ["c:ping", "c:protocols", "c:server_port", "c:player_ping", "c:player_profiles_list"];
}