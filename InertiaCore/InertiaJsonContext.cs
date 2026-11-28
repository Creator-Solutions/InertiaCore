using System.Text.Json.Serialization;
using InertiaCore.Models;

namespace InertiaCore;

[JsonSerializable(typeof(Page))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(ScrollPropEntry))]
[JsonSerializable(typeof(OncePropEntry))]
[JsonSerializable(typeof(Dictionary<string, ScrollPropEntry>))]
[JsonSerializable(typeof(Dictionary<string, List<string>>))]
internal partial class InertiaJsonContext : JsonSerializerContext
{
}