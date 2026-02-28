using Sheetly.CLI.Helpers;
using System.CommandLine;

namespace Sheetly.CLI.Commands;

public class UpdateCommand : Command
{
	private readonly Option<string?> _projectOption = new("--project", ["-p"]) { Description = "Manual path to DLL" };
	private readonly Option<bool> _noBuildOption = new("--no-build", ["-n"]) { Description = "Do not build project before action" };

	public UpdateCommand() : base("update", "Apply migrations to the database")
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

			Console.WriteLine("⏳ Applying pending migrations...");
			var json = CliHelper.InvokeDesignTime(coreAsm, "UpdateDatabaseAsync", contextType, connStr);
			var doc = CliHelper.ParseResult(json);
			if (doc is null) return;

			var root = doc.RootElement;
			int total = root.GetProperty("total").GetInt32();

			if (total == 0)
			{
				Console.WriteLine("✅ Database is up to date.");
				return;
			}

			foreach (var m in root.GetProperty("applied").EnumerateArray())
				Console.WriteLine($"  Applied: {m.GetString()}");

			Console.WriteLine($"✅ {total} migration(s) applied successfully.");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Error: {ex.Message}");
			if (ex.InnerException is not null) Console.WriteLine($"🔍 Detail: {ex.InnerException.Message}");
		}
	}
}
