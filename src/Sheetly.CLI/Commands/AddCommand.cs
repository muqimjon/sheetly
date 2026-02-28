using Sheetly.CLI.Helpers;
using System.CommandLine;

namespace Sheetly.CLI.Commands;

public class AddCommand : Command
{
	private readonly Argument<string?> _nameArg = new("name") { Arity = ArgumentArity.ZeroOrOne, Description = "Migration name" };
	private readonly Option<string?> _projectOption = new("--project", ["-p"]);
	private readonly Option<bool> _noBuildOption = new("--no-build", ["-n"]);
	private readonly Option<string?> _outputDirOption = new("--output-dir", ["-o"]);

	public AddCommand() : base("add", "Add a new migration")
	{
		this.Add(_nameArg);
		this.Add(_projectOption);
		this.Add(_noBuildOption);
		this.Add(_outputDirOption);

		this.SetAction(async (parseResult, ct) =>
		{
			string? name = parseResult.GetValue(_nameArg);
			bool noBuild = parseResult.GetValue(_noBuildOption);
			string? projectPath = parseResult.GetValue(_projectOption);
			string? outputDir = parseResult.GetValue(_outputDirOption);
			await ExecuteAsync(name, noBuild, projectPath, outputDir, ct);
		});
	}

	private async Task ExecuteAsync(string? name, bool noBuild, string? projectPath, string? outputDir, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			Console.Write("Enter migration name: ");
			name = Console.ReadLine();
			if (string.IsNullOrWhiteSpace(name)) return;
		}

		string dllPath = CliHelper.FindProjectDll(noBuild, projectPath);
		if (string.IsNullOrEmpty(dllPath)) return;

		try
		{
			var (assembly, loadContext) = CliHelper.LoadAssemblyIsolated(dllPath);
			var coreAsm = CliHelper.GetCoreAssembly(assembly, loadContext);
			var contextType = CliHelper.FindContextType(assembly);

			var json = CliHelper.InvokeDesignTime(coreAsm, "AddMigration", contextType, name, outputDir);
			var doc = CliHelper.ParseResult(json);
			if (doc is null) return;

			var root = doc.RootElement;
			Console.WriteLine($"✅ Migration created: '{root.GetProperty("migrationFile").GetString()}'");
			Console.WriteLine($"✅ Model snapshot updated: '{root.GetProperty("snapshotFile").GetString()}'");

			var ops = root.GetProperty("operations");
			Console.WriteLine($"   Operations: {ops.GetArrayLength()}");
			foreach (var op in ops.EnumerateArray())
				Console.WriteLine($"   - {op.GetString()}");
		}
		catch (Exception ex) { Console.WriteLine($"❌ Error: {ex.Message}"); }
	}
}
