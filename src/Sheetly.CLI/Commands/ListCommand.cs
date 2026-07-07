using Sheetly.CLI.Helpers;
using System.CommandLine;

namespace Sheetly.CLI.Commands;

/// <summary>
/// Lists all migrations and their applied/pending status against the database.
/// </summary>
public class ListCommand : Command
{
	private readonly Option<string?> _projectOption = new("--project", ["-p"]) { Description = "Path to project" };

	public ListCommand() : base("list", "List all migrations and their status")
	{
		this.Add(_projectOption);
		this.SetAction(async (parseResult, ct) => await ExecuteAsync(parseResult.GetValue(_projectOption), ct));
	}

	private async Task ExecuteAsync(string? projectPath, CancellationToken ct)
	{
		string dllPath = CliHelper.FindProjectDll(false, projectPath);
		if (string.IsNullOrEmpty(dllPath)) return;

		try
		{
			var (assembly, loadContext) = CliHelper.LoadAssemblyIsolated(dllPath);
			var coreAsm = CliHelper.GetCoreAssembly(assembly, loadContext);
			var contextType = CliHelper.FindContextType(assembly);

			var json = CliHelper.InvokeDesignTime(coreAsm, "GetMigrationsStatusAsync", contextType, null);
			var doc = CliHelper.ParseResult(json);
			if (doc is null) return;

			var root = doc.RootElement;
			var migrations = root.GetProperty("migrations");

			Console.WriteLine("📋 Migrations:");
			Console.WriteLine();

			if (migrations.GetArrayLength() == 0)
			{
				Console.WriteLine("   No migrations found.");
				return;
			}

			int appliedCount = 0;
			foreach (var m in migrations.EnumerateArray())
			{
				var id = m.GetProperty("id").GetString();
				bool isApplied = m.GetProperty("applied").GetBoolean();
				if (isApplied) appliedCount++;
				Console.WriteLine(isApplied ? $"   ✓ {id} (applied)" : $"   • {id} (pending)");
			}

			Console.WriteLine();
			if (!root.GetProperty("providerReachable").GetBoolean())
				Console.WriteLine("⚠️  Could not reach the database — status shown from local files only (all pending).");
			Console.WriteLine($"Total: {migrations.GetArrayLength()} migration(s), {appliedCount} applied.");
		}
		catch (Exception ex) { Console.WriteLine($"❌ Error: {ex.Message}"); }

		await Task.CompletedTask;
	}
}
