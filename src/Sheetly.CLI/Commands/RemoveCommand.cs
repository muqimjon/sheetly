using Sheetly.CLI.Helpers;
using System.CommandLine;

namespace Sheetly.CLI.Commands;

public class RemoveCommand : Command
{
	private readonly Option<string?> _projectOption = new("--project", ["-p"]);
	private readonly Option<bool> _forceOption = new("--force", ["-f"]) { Description = "Remove even if the migration is applied to the database." };

	public RemoveCommand() : base("remove", "Remove the last migration")
	{
		this.Add(_projectOption);
		this.Add(_forceOption);
		this.SetAction(async (parseResult, ct) => await ExecuteAsync(parseResult.GetValue(_projectOption), parseResult.GetValue(_forceOption), ct));
	}

	private async Task ExecuteAsync(string? projectPath, bool force, CancellationToken ct)
	{
		string dllPath = CliHelper.FindProjectDll(false, projectPath);
		if (string.IsNullOrEmpty(dllPath)) return;

		try
		{
			var (assembly, loadContext) = CliHelper.LoadAssemblyIsolated(dllPath);
			var coreAsm = CliHelper.GetCoreAssembly(assembly, loadContext);
			var contextType = CliHelper.FindContextType(assembly);

			var json = CliHelper.InvokeDesignTime(coreAsm, "RemoveMigration", contextType, force);
			var doc = CliHelper.ParseResult(json);
			if (doc is null) return;

			var root = doc.RootElement;
			Console.WriteLine($"✅ Migration removed: '{root.GetProperty("removedFile").GetString()}'");
			Console.WriteLine($"✅ Model snapshot reverted: '{root.GetProperty("snapshotFile").GetString()}'");
		}
		catch (Exception ex) { Console.WriteLine($"❌ Error: {ex.Message}"); }
	}
}