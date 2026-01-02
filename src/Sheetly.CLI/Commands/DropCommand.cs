using System.CommandLine;
using System.Reflection;
using Sheetly.Core;
using Sheetly.Google;
using Sheetly.CLI.Helpers;

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
			Console.Write("⚠️ Ma'lumotlar bazasini tozalashga ishonchingiz komilmi? (y/N): ");
			if (Console.ReadLine()?.ToLower() != "y") return;
		}

		string dllPath = CliHelper.FindProjectDll(true, projectPath);
		if (string.IsNullOrEmpty(dllPath)) return;

		try
		{
			var assembly = Assembly.LoadFrom(Path.GetFullPath(dllPath));
			var contextType = assembly.GetExportedTypes().FirstOrDefault(t => CliHelper.IsSubclassOfSheetsContext(t))
				?? throw new Exception("SheetsContext topilmadi.");

			string? connStr = CliHelper.GetConnectionString(CliHelper.FindProjectRootFromDll(dllPath));

			var method = typeof(GoogleSheetsFactory).GetMethods()
				.FirstOrDefault(m => m.Name == "CreateContextAsync" && m.GetParameters().Length == 1)
				?.MakeGenericMethod(contextType);

			var task = (Task)method!.Invoke(null, [connStr])!;
			await task;

			var context = (SheetsContext)((dynamic)task).Result;
			await context.Database.DropDatabaseAsync();
			Console.WriteLine("✅ Ma'lumotlar bazasi tozalandi.");
		}
		catch (Exception ex) { Console.WriteLine($"❌ Xato: {ex.Message}"); }
	}
}
