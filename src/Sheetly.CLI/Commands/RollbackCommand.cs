using Sheetly.CLI.Helpers;
using System.CommandLine;

namespace Sheetly.CLI.Commands;

/// <summary>
/// Reverts the last applied migration against the database (runs its Down()).
/// The local migration file is kept — only the database state and history change.
/// </summary>
public class RollbackCommand : Command
{
	private readonly Option<string?> _projectOption = new("--project", ["-p"]) { Description = "Manual path to DLL" };
	private readonly Option<bool> _noBuildOption = new("--no-build", ["-n"]) { Description = "Do not build project before action" };

	public RollbackCommand() : base("rollback", "Revert the last applied migration")
	{
		this.Add(_projectOption);
		this.Add(_noBuildOption);
		this.SetAction(async (parseResult, ct) =>
		{
			bool noBuild = parseResult.GetValue(_noBuildOption);
			string? projectPath = parseResult.GetValue(_projectOption);
			await ExecuteAsync(noBuild, projectPath, ct);
		});
	}

	private async Task ExecuteAsync(bool noBuild, string? projectPath, CancellationToken ct)
	{
		string dllPath = CliHelper.FindProjectDll(noBuild, projectPath);
		if (string.IsNullOrEmpty(dllPath)) return;

		try
		{
			var (assembly, loadContext) = CliHelper.LoadAssemblyIsolated(dllPath);
			var coreAsm = CliHelper.GetCoreAssembly(assembly, loadContext);
			var contextType = CliHelper.FindContextType(assembly);

			string? connStr = CliHelper.GetConnectionString(CliHelper.FindProjectRootFromDll(dllPath));

			Console.WriteLine("⏳ Reverting last migration...");
			var json = CliHelper.InvokeDesignTime(coreAsm, "RollbackDatabaseAsync", contextType, connStr);
			var doc = CliHelper.ParseResult(json);
			if (doc is null) return;

			var rolledBack = doc.RootElement.GetProperty("rolledBack").GetString();
			if (string.IsNullOrEmpty(rolledBack))
				Console.WriteLine("✅ No applied migrations to revert.");
			else
				Console.WriteLine($"✅ Reverted: {rolledBack}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Error: {ex.Message}");
			if (ex.InnerException is not null) Console.WriteLine($"🔍 Detail: {ex.InnerException.Message}");
		}
	}
}
