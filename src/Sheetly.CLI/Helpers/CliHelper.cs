using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Text.Json;

namespace Sheetly.CLI.Helpers;

public static class CliHelper
{
	private const string DesignTimeType = "Sheetly.Core.Migrations.Design.DesignTimeOperations";

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

	/// <summary>
	/// Invokes a static method on DesignTimeOperations inside the isolated context.
	/// Returns the raw string result (JSON). Only strings cross the boundary.
	/// This mirrors EF Core's OperationExecutor pattern.
	/// </summary>
	internal static string InvokeDesignTime(Assembly coreAsm, string methodName, params object?[] args)
	{
		var designType = coreAsm.GetType(DesignTimeType)
			?? throw new Exception(VersionMismatchMessage(coreAsm, DesignTimeType));
		var method = designType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
			?? throw new Exception(VersionMismatchMessage(coreAsm, methodName));

		var result = method.Invoke(null, args);

		// Handle async methods (Task<string>)
		if (result is Task task)
		{
			task.GetAwaiter().GetResult();
			return (string)((dynamic)task).Result;
		}

		return (string)result!;
	}

	/// <summary>
	/// Finds SheetsContext subclass from the loaded assembly using string-based type check.
	/// </summary>
	internal static Type FindContextType(Assembly assembly)
	{
		return assembly.GetExportedTypes().FirstOrDefault(t => IsSubclassOfSheetsContext(t))
			?? throw new Exception("SheetsContext not found in the project.");
	}

	/// <summary>
	/// Parses a JSON result string from DesignTimeOperations and prints error if unsuccessful.
	/// Returns the parsed JsonDocument, or null on failure.
	/// </summary>
	internal static JsonDocument? ParseResult(string json)
	{
		var doc = JsonDocument.Parse(json);
		if (doc.RootElement.GetProperty("success").GetBoolean())
			return doc;

		var error = doc.RootElement.GetProperty("error").GetString();
		Console.WriteLine($"❌ Error: {error}");
		return null;
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

	/// <summary>
	/// Produces a human-friendly "update the CLI" message when a reflection lookup fails.
	/// </summary>
	public static string VersionMismatchMessage(Assembly coreAsm, string missingMember)
	{
		var projectVer = coreAsm.GetName().Version?.ToString() ?? "unknown";
		return $"Incompatible Sheetly.Core version ({projectVer}): member '{missingMember}' not found.\n" +
			   $"Run: dotnet tool update -g dotnet-sheetly";
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
}