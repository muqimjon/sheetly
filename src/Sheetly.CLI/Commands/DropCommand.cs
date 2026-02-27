using Sheetly.CLI.Helpers;
using System.CommandLine;
using System.Reflection;

namespace Sheetly.CLI.Commands;

public class DropCommand : Command
{
	private readonly Option<bool> _forceOption = new("--force", ["-f"]);
	private readonly Option<string?> _projectOption = new("--project", ["-p"]);

	public DropCommand() : base("drop", "Drop the database (clear sheets)")
	{
		this.Add(_forceOption);
		this.Add(_projectOption);
		this.SetAction(async (parseResult, ct) => await ExecuteAsync(
			parseResult.GetValue(_forceOption),
			parseResult.GetValue(_projectOption)));
	}

	private async Task ExecuteAsync(bool force, string? projectPath)
	{
		if (!force)
		{
			Console.Write("⚠️ Are you sure you want to drop the database? (y/N): ");
			if (Console.ReadLine()?.ToLower() != "y") return;
		}

		string dllPath = CliHelper.FindProjectDll(true, projectPath);
		if (string.IsNullOrEmpty(dllPath)) return;

		try
		{
			var (assembly, loadContext) = CliHelper.LoadAssemblyIsolated(dllPath);
			var contextType = assembly.GetExportedTypes().FirstOrDefault(t => CliHelper.IsSubclassOfSheetsContext(t))
				?? throw new Exception("SheetsContext not found.");

			string? connStr = CliHelper.GetConnectionString(CliHelper.FindProjectRootFromDll(dllPath))
				?? CliHelper.GetConnectionStringFromContext(contextType);

			// Use isolated Sheetly.Google factory so contextType satisfies its T : SheetsContext constraint
			var googleAsm = CliHelper.GetGoogleAssembly(assembly, loadContext)
				?? throw new Exception("Sheetly.Google not found in project references.");
			var factoryType = googleAsm.GetType("Sheetly.Google.GoogleSheetsFactory")!;

			var method = factoryType.GetMethods()
				.FirstOrDefault(m => m.Name == "CreateContextAsync" && m.GetParameters().Length == 1)
				?.MakeGenericMethod(contextType);

			var task = (Task)method!.Invoke(null, [connStr])!;
			await task;

			dynamic context = ((dynamic)task).Result;
			await context.Database.DropDatabaseAsync();
			Console.WriteLine("✅ Database dropped successfully.");
		}
		catch (Exception ex) { Console.WriteLine($"❌ Error: {ex.Message}"); }
	}
}
