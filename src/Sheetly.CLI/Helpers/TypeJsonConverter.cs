using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sheetly.CLI.Helpers;

/// <summary>
/// Serializes System.Type as its AssemblyQualifiedName so that MigrationOperation
/// fields like ClrType survive JSON round-tripping across AssemblyLoadContext boundaries.
/// </summary>
internal sealed class TypeJsonConverter : JsonConverter<Type?>
{
	public override Type? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var name = reader.GetString();
		if (string.IsNullOrEmpty(name)) return typeof(string);
		// AssemblyQualifiedName resolves for all BCL types
		return Type.GetType(name) ?? typeof(string);
	}

	public override void Write(Utf8JsonWriter writer, Type? value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value?.AssemblyQualifiedName ?? typeof(string).AssemblyQualifiedName);
	}
}
