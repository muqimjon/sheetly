using Microsoft.Extensions.Configuration;
using Sheetly.Core.Migration;
using Sheetly.Core.Migrations.Operations;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sheetly.CLI.Helpers;

public static class CliHelper
{
	// JSON options used when bridging objects across AssemblyLoadContext boundaries.
	// TypeJsonConverter handles System.Type fields (e.g. ClrType on operation classes).
	private static readonly JsonSerializerOptions _bridgeOptions = new()
	{
		Converters = { new TypeJsonConverter() }
	};

	/// <summary>Loads the project DLL and its dependencies into an isolated context.</summary>
	internal static (Assembly assembly, ProjectAssemblyLoadContext loadContext) LoadAssemblyIsolated(string dllPath)
	{
		var fullPath = Path.GetFullPath(dllPath);
		var ctx = new ProjectAssemblyLoadContext(fullPath);
		return (ctx.LoadFromAssemblyPath(fullPath), ctx);
	}

	/// <summary>Resolves Sheetly.Core from the project's isolated load context.</summary>
	internal static Assembly GetCoreAssembly(Assembly userAssembly, ProjectAssemblyLoadContext loadContext)
	{
		var coreRef = userAssembly.GetReferencedAssemblies()
			.FirstOrDefault(a => a.Name == "Sheetly.Core")
			?? throw new Exception("Sheetly.Core not found in assembly references.");
		return loadContext.LoadFromAssemblyName(coreRef);
	}

	/// <summary>Resolves Sheetly.Google from the project's isolated load context (may be null).</summary>
	internal static Assembly? GetGoogleAssembly(Assembly userAssembly, ProjectAssemblyLoadContext loadContext)
	{
		var googleRef = userAssembly.GetReferencedAssemblies().FirstOrDefault(a => a.Name == "Sheetly.Google");
		return googleRef != null ? loadContext.LoadFromAssemblyName(googleRef) : null;
	}

	/// <summary>
	/// String-based IsSubclassOf that works across AssemblyLoadContext boundaries
	/// (type identity is context-scoped, so reference comparison fails cross-context).
	/// </summary>
	public static bool IsSubclassOf(Type? type, string baseTypeFullName)
	{
		while (type != null && type != typeof(object))
		{
			if (type.FullName == baseTypeFullName) return true;
			type = type.BaseType;
		}
		return false;
	}

	/// <summary>
	/// Produces a human-friendly "update the CLI" message when a reflection lookup fails,
	/// indicating that the project's Sheetly.Core version is incompatible with this CLI.
	/// </summary>
	public static string VersionMismatchMessage(Assembly coreAsm, string missingMember)
	{
		var projectVer = coreAsm.GetName().Version?.ToString() ?? "unknown";
		return $"Incompatible Sheetly.Core version ({projectVer}): member '{missingMember}' not found.\n" +
			   $"Run: dotnet tool update -g dotnet-sheetly";
	}


	public static string? GetMigrationAttributeId(Type t)
	{
		var attr = t.GetCustomAttributes(false).FirstOrDefault(a => a.GetType().Name == "MigrationAttribute");
		return attr?.GetType().GetProperty("Id")?.GetValue(attr) as string;
	}

	/// <summary>
	/// Serializes an object from the isolated context to JSON and deserializes it
	/// into a CLI-side type, crossing the AssemblyLoadContext boundary safely.
	/// </summary>
	public static T? BridgeFromJson<T>(object? isolatedObj) where T : class
	{
		if (isolatedObj == null) return null;
		var json = JsonSerializer.Serialize(isolatedObj, isolatedObj.GetType(), _bridgeOptions);
		return JsonSerializer.Deserialize<T>(json, _bridgeOptions);
	}

	/// <summary>
	/// Bridges a List&lt;MigrationOperation&gt; across context boundaries.
	/// Performs manual dispatch on OperationType because MigrationOperation is abstract.
	/// </summary>
	public static List<MigrationOperation> BridgeMigrationOperations(object? isolatedOps)
	{
		if (isolatedOps == null) return [];
		var json = JsonSerializer.Serialize(isolatedOps, isolatedOps.GetType(), _bridgeOptions);
		var elements = JsonSerializer.Deserialize<List<JsonObject>>(json, _bridgeOptions);
		if (elements == null) return [];

		var result = new List<MigrationOperation>();
		foreach (var el in elements)
		{
			var opType = el["OperationType"]?.GetValue<string>();
			MigrationOperation? op = opType switch
			{
				"CreateTable"        => el.Deserialize<CreateTableOperation>(_bridgeOptions),
				"AddColumn"          => el.Deserialize<AddColumnOperation>(_bridgeOptions),
				"DropColumn"         => el.Deserialize<DropColumnOperation>(_bridgeOptions),
				"DropTable"          => el.Deserialize<DropTableOperation>(_bridgeOptions),
				"AlterColumn"        => el.Deserialize<AlterColumnOperation>(_bridgeOptions),
				"CreateIndex"        => el.Deserialize<CreateIndexOperation>(_bridgeOptions),
				"DropIndex"          => el.Deserialize<DropIndexOperation>(_bridgeOptions),
				"AddCheckConstraint" => el.Deserialize<AddCheckConstraintOperation>(_bridgeOptions),
				"DropCheckConstraint"=> el.Deserialize<DropCheckConstraintOperation>(_bridgeOptions),
				_                    => null
			};
			if (op != null) result.Add(op);
		}
		return result;
	}


	public static bool IsSubclassOfSheetsContext(Type? type)
	{
		while (type != null && type != typeof(object))
		{
			if (type.FullName == "Sheetly.Core.SheetsContext") return true;
			type = type.BaseType;
		}
		return false;
	}

	public static string FindProjectDll(bool noBuild, string? manualPath)
	{
		if (!string.IsNullOrEmpty(manualPath)) return manualPath;
		var csproj = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj").FirstOrDefault();
		if (csproj == null) return string.Empty;

		if (!noBuild)
		{
			Console.WriteLine($"⏳ Building project: '{Path.GetFileName(csproj)}'...");
			var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
				"dotnet", $"build \"{csproj}\" -c Debug --no-self-contained")
			{
				UseShellExecute = false
			});
			process?.WaitForExit();
			if (process?.ExitCode != 0)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("❌ Build failed. Fix the errors above before running this command.");
				Console.ResetColor();
				return string.Empty;
			}
		}

		var targetDir = Path.Combine(Directory.GetCurrentDirectory(), "bin", "Debug");
		var dllFiles = Directory.GetFiles(targetDir, $"{Path.GetFileNameWithoutExtension(csproj)}.dll", SearchOption.AllDirectories);
		return dllFiles.OrderByDescending(File.GetLastWriteTime).FirstOrDefault() ?? string.Empty;
	}

	public static string FindProjectRootFromDll(string dllPath)
	{
		var dir = new DirectoryInfo(Path.GetDirectoryName(dllPath)!);
		while (dir != null && !dir.GetFiles("*.csproj").Any()) dir = dir.Parent;
		return dir?.FullName ?? Path.GetDirectoryName(dllPath)!;
	}

	public static string? GetConnectionString(string projectDir)
	{
		var config = new ConfigurationBuilder()
			.SetBasePath(projectDir)
			.AddJsonFile("appsettings.json", true)
			.AddJsonFile("appsettings.Development.json", true)
			.Build();

		return config.GetConnectionString("DefaultConnection")
			   ?? config.GetSection("Sheetly")["ConnectionString"];
	}

	public static string? GetConnectionStringFromContext(Type contextType)
	{
		try
		{
			var context = Activator.CreateInstance(contextType);
			var options = new Sheetly.Core.Configuration.SheetsOptions();
			var method = contextType.GetMethod("OnConfiguring",
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			method?.Invoke(context, [options]);
			return options.ConnectionString;
		}
		catch { return null; }
	}

	public static string GenerateClassCode(EntitySchema entity)
	{
		var sb = new StringBuilder();
		sb.AppendLine("using System.ComponentModel.DataAnnotations;");
		sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
		sb.AppendLine();
		sb.AppendLine($"namespace {entity.Namespace}.Scaffolded;");
		sb.AppendLine();
		sb.AppendLine($"[Table(\"{entity.TableName}\")]");
		sb.AppendLine($"public class {entity.ClassName}");
		sb.AppendLine("{");
		foreach (var col in entity.Columns)
		{
			if (col.IsPrimaryKey) sb.AppendLine("    [Key]");
			if (col.IsForeignKey) sb.AppendLine($"    [ForeignKey(\"{col.ForeignKeyTable}\")]");
			var type = col.DataType;
			if (col.IsNullable && type != "String" && !type.EndsWith("?")) type += "?";
			sb.AppendLine($"    public {type} {col.PropertyName} {{ get; set; }}");
			sb.AppendLine();
		}
		sb.AppendLine("}");
		return sb.ToString();
	}
}