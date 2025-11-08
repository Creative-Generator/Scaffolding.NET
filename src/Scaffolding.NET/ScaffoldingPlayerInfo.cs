using System.Net;
using System.Text.Json.Serialization;

namespace Scaffolding.NET;

public record ScaffoldingPlayerInfo
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("machine_id")] public string MachineId { get; set; } = string.Empty;
    [JsonPropertyName("easytier_id")] public string? EasyTierId { get; set; }
    [JsonPropertyName("vendor")] public string Vendor { get; set; } = string.Empty;
    [JsonPropertyName("kind")] public ScaffoldingPlayerKind Kind { get; set; }
}