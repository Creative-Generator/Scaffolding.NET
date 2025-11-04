using Scaffolding.NET.EasyTier;

namespace Scaffolding.NET;

public record ScaffoldingClientOptions
{
    public required string RoomId { get; set; }
    public required string MachineId { get; set; }
    public required string PlayerName { get; set; }
    public string Vendor { get; set; } = "Scaffolding.NET";
    public required EasyTierFileInfo EasyTierFileInfo { get; set; }
    public EasyTierStartInfo? EasyTierStartInfo { get; set; }
}