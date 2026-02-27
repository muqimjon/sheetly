using Sheetly.CLI.Helpers;
using System.CommandLine;

namespace Sheetly.CLI.Commands;

public class ScriptCommand : Command
{
	private readonly Option<string?> _projectOption = new("--project", ["-p"]) { Description = "Manual path to DLL" };
	private readonly Option<bool> _noBuildOption = new("--no-build", ["-n"]) { Description = "Do not build project" };

	public ScriptCommand() : base("script", "Generate a schema script from the latest snapshot")
	{
		this.Add(_projectOption);
		this.Add(_noBuildOption);
		this.SetAction(async (parseResult, ct) => await ExecuteAsync(
			parseResult.GetValue(_noBuildOption),
			parseResult.GetValue(_projectOption)));
	}

	private async Task ExecuteAsync(bool noBuild, string? projectPath)
	{
		string dllPath = CliHelper.FindProjectDll(noBuild, projectPath);
		if (string.IsNullOrEmpty(dllPath)) return;

		try
		{
			var (assembly, loadContext) = CliHelper.LoadAssemblyIsolated(dllPath);
			var coreAsm = CliHelper.GetCoreAssembly(assembly, loadContext);
			var contextType = CliHelper.FindContextType(assembly);

			var json = CliHelper.InvokeDesignTime(coreAsm, "GetSchemaScript", contextType);
			var doc = CliHelper.ParseResult(json);
			if (doc == null) return;

			Console.Write(doc.RootElement.GetProperty("script").GetString());
		}
		catch (Exception ex) { Console.WriteLine($"❌ Error: {ex.Message}"); }
	}
}