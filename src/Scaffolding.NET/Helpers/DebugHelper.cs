using System.Diagnostics;

namespace Scaffolding.NET.Helpers;

public static class DebugHelper
{
    public static void WriteLine(object value) => Debug.WriteLine($"[Scaffolding.NET] {value}");
}