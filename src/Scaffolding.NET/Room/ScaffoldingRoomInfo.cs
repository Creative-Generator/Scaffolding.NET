namespace Scaffolding.NET.Room;

public record ScaffoldingRoomInfo
{
    public required ScaffoldingPlayerInfo Host { get; init; }
    public required IReadOnlyList<ScaffoldingPlayerInfo> PlayerInfos { get; init; }
    public ushort? ServerPort { get; internal set; }
}