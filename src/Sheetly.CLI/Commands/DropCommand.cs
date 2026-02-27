using Sheetly.CLI.Helpers;
using System.CommandLine;

namespace Sheetly.CLI.Commands;

public class DropCommand : Command
{
	private readonly Option<bool> _forceOption = new("--force", ["-f"]);
	private readonly Option<string?> _projectOption = new("--project", ["-p"]);
	private readonly Option<bool> _noBuildOption = new("--no-build", ["-n"]) { Description = "Do not build project" };

	public DropCommand() : base("drop", "Drop the database (clear sheets)")
	{
		this.Add(_forceOption);
		this.Add(_projectOption);
		this.Add(_noBuildOption);
		this.SetAction(async (parseResult, ct) => await ExecuteAsync(
			parseResult.GetValue(_forceOption),
			parseResult.GetValue(_noBuildOption),
			parseResult.GetValue(_projectOption)));
	}

	private async Task ExecuteAsync(bool force, bool noBuild, string? projectPath)
	{
		if (!force)
		{
			Console.Write("⚠️ Are you sure you want to drop the database? (y/N): ");
			if (Console.ReadLine()?.ToLower() != "y") return;
		}

		string dllPath = CliHelper.FindProjectDll(noBuild, projectPath);
		if (string.IsNullOrEmpty(dllPath)) return;

		try
		{
			var (assembly, loadContext) = CliHelper.LoadAssemblyIsolated(dllPath);
			var coreAsm = CliHelper.GetCoreAssembly(assembly, loadContext);
			var contextType = CliHelper.FindContextType(assembly);

			string? connStr = CliHelper.GetConnectionString(CliHelper.FindProjectRootFromDll(dllPath));

			var json = CliHelper.InvokeDesignTime(coreAsm, "DropDatabaseAsync", contextType, connStr);
			var doc = CliHelper.ParseResult(json);
			if (doc == null) return;

			Console.WriteLine("✅ Database dropped successfully.");
		}
		catch (Exception ex) { Console.WriteLine($"❌ Error: {ex.Message}"); }
	}
}
