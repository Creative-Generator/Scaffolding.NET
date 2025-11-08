using System.Text.Json.Serialization;

namespace Scaffolding.NET;

[JsonConverter(typeof(JsonStringEnumConverter<ScaffoldingPlayerKind>))]
public enum ScaffoldingPlayerKind
{
    Host,
    Guest
}