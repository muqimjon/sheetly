using Sheetly.CLI.Helpers;
using System.CommandLine;

namespace Sheetly.CLI.Commands;

public class RemoveCommand : Command
{
	private readonly Option<string?> _projectOption = new("--project", ["-p"]);

	public RemoveCommand() : base("remove", "Remove the last migration")
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

			var json = CliHelper.InvokeDesignTime(coreAsm, "RemoveMigration", contextType);
			var doc = CliHelper.ParseResult(json);
			if (doc == null) return;

			var root = doc.RootElement;
			Console.WriteLine($"✅ Migration removed: '{root.GetProperty("removedFile").GetString()}'");
			Console.WriteLine($"✅ Model snapshot reverted: '{root.GetProperty("snapshotFile").GetString()}'");
		}
		catch (Exception ex) { Console.WriteLine($"❌ Error: {ex.Message}"); }
	}
}