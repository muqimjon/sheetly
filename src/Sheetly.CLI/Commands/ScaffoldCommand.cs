using Sheetly.CLI.Helpers;
using System.CommandLine;

namespace Sheetly.CLI.Commands;

public class ScaffoldCommand : Command
{
	private readonly Option<string?> _projectOption = new("--project", ["-p"]);
	private readonly Option<string?> _outputDirOption = new("--output-dir", ["-o"]);
	private readonly Option<bool> _noBuildOption = new("--no-build", ["-n"]) { Description = "Do not build project" };

	public ScaffoldCommand() : base("scaffold", "Scaffold model classes from remote provider")
	{
		this.Add(_projectOption);
		this.Add(_outputDirOption);
		this.Add(_noBuildOption);
		this.SetAction(async (parseResult, ct) => await ExecuteAsync(
			parseResult.GetValue(_noBuildOption),
			parseResult.GetValue(_projectOption),
			parseResult.GetValue(_outputDirOption),
			ct));
	}

	private async Task ExecuteAsync(bool noBuild, string? projectPath, string? outputDir, CancellationToken ct)
	{
		string dllPath = CliHelper.FindProjectDll(noBuild, projectPath);
		if (string.IsNullOrEmpty(dllPath)) return;

		try
		{
			var (assembly, loadContext) = CliHelper.LoadAssemblyIsolated(dllPath);
			var coreAsm = CliHelper.GetCoreAssembly(assembly, loadContext);
			var contextType = CliHelper.FindContextType(assembly);

			string? connStr = CliHelper.GetConnectionString(CliHelper.FindProjectRootFromDll(dllPath));

			Console.WriteLine("⏳ Scaffolding models from remote provider...");
			var json = CliHelper.InvokeDesignTime(coreAsm, "ScaffoldAsync", contextType, outputDir, connStr);
			var doc = CliHelper.ParseResult(json);
			if (doc == null) return;

			foreach (var f in doc.RootElement.GetProperty("files").EnumerateArray())
				Console.WriteLine($"📄 Created: {f.GetString()}");

			Console.WriteLine("✅ Scaffolding complete.");
		}
		catch (Exception ex) { Console.WriteLine($"❌ Error: {ex.Message}"); }
	}
}