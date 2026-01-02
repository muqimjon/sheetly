using Sheetly.Core.Abstractions;
using System.Text;

namespace Sheetly.Core.Design;

public class ScaffoldService(ISheetsProvider provider)
{
	public async Task<string> GenerateEntitiesAsync(string @namespace)
	{
		var rows = await provider.GetAllRowsAsync("__SheetlySchema__");
		if (rows.Count <= 1) return string.Empty;

		var tables = rows.Skip(1)
			.GroupBy(r => r[0].ToString())
			.ToDictionary(g => g.Key!, g => g.ToList());

		var sb = new StringBuilder();
		sb.AppendLine($"namespace {@namespace}.Entities;");
		sb.AppendLine();

		foreach (var table in tables)
		{
			sb.AppendLine($"public class {table.Key}");
			sb.AppendLine("{");
			foreach (var col in table.Value)
			{
				string sheetType = col[2].ToString()!;
				string propName = col[1].ToString()!;
				string constraints = col[3].ToString()!;

				bool isNullable = !constraints.Contains("Required");
				string csharpType = MapToCSharpType(sheetType, isNullable);

				if (constraints.Contains("PK")) sb.AppendLine("    [Key]");

				sb.AppendLine($"    public {csharpType} {propName} {{ get; set; }}");
			}
			sb.AppendLine("}");
			sb.AppendLine();
		}

		return sb.ToString();
	}

	public async Task<string> GenerateContextAsync(string @namespace, string contextName)
	{
		var rows = await provider.GetAllRowsAsync("__SheetlySchema__");
		var tableNames = rows.Skip(1).Select(r => r[0].ToString()).Distinct();

		var sb = new StringBuilder();
		sb.AppendLine("using Sheetly.Core;");
		sb.AppendLine($"using {@namespace}.Entities;");
		sb.AppendLine();
		sb.AppendLine($"namespace {@namespace};");
		sb.AppendLine();
		sb.AppendLine($"public class {contextName} : SheetsContext");
		sb.AppendLine("{");

		foreach (var name in tableNames)
		{
			sb.AppendLine($"    public SheetsSet<{name}> {name}s {{ get; set; }}");
		}

		sb.AppendLine("}");
		return sb.ToString();
	}

	private string MapToCSharpType(string sheetType, bool isNullable)
	{
		var type = sheetType switch
		{
			"Int16" => "short",
			"Int32" => "int",
			"Int64" => "long",
			"Decimal" => "decimal",
			"Double" => "double",
			"Single" => "float",
			"Boolean" => "bool",
			"DateTime" => "DateTime",
			"DateTimeOffset" => "DateTimeOffset",
			"TimeSpan" => "TimeSpan",
			"Guid" => "Guid",
			_ => "string"
		};

		return (isNullable && type != "string") ? $"{type}?" : type;
	}
}