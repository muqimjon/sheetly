using System.CommandLine;
using Sheetly.CLI.Helpers;

namespace Sheetly.CLI.Commands;

public class RemoveCommand : Command
{
	private readonly Option<string?> _projectOption = new("--project", ["-p"]);

	public RemoveCommand() : base("remove", "Remove the last migration")
	{
		this.Add(_projectOption);
		this.SetAction(async (parseResult, ct) => await ExecuteAsync(parseResult.GetValue(_projectOption)));
	}

	private async Task ExecuteAsync(string? projectPath)
	{
		string dllPath = CliHelper.FindProjectDll(true, projectPath);
		if (string.IsNullOrEmpty(dllPath)) return;

		try
		{
			string contextProjectDir = CliHelper.FindProjectRootFromDll(Path.GetFullPath(dllPath));
			string migrationsDir = Path.Combine(contextProjectDir, "Migrations");

			if (!Directory.Exists(migrationsDir)) return;

			var files = Directory.GetFiles(migrationsDir, "*.json")
				.Where(f => !f.EndsWith("sheetly_snapshot.json"))
				.OrderByDescending(f => f)
				.ToList();

			if (files.Count > 0)
			{
				File.Delete(files[0]);
				Console.WriteLine($"✅ O'chirildi: {Path.GetFileName(files[0])}");
			}
		}
		catch (Exception ex) { Console.WriteLine($"❌ Xato: {ex.Message}"); }
	}
}