using System.Text.Json.Serialization;

namespace Scaffolding.NET.EasyTier;

public record EasyTierPeerInfo
{
    [JsonPropertyName("hostname")] public string Hostname { get; set; } = string.Empty;
    [JsonPropertyName("ipv4")] public string Ipv4 { get; set; } = string.Empty;
    [JsonPropertyName("cost")] public string Cost { get; set; } = string.Empty;
    [JsonPropertyName("lat_ms")] public string Ping { get; set; } = string.Empty;
    [JsonPropertyName("loss_rate")] public string Loss { get; set; } = string.Empty;
    [JsonPropertyName("nat_type")] public string NatType { get; set; } = string.Empty;
    [JsonPropertyName("version")] public string EasyTierVersion { get; set; } = string.Empty;
}