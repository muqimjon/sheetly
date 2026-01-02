using Sheetly.CLI.Helpers;
using Sheetly.Core.Migration;
using System.CommandLine;
using System.Text.Json;

namespace Sheetly.CLI.Commands;

public class ScriptCommand : Command
{
	private readonly Option<string?> _projectOption = new("--project", ["-p"]) { Description = "Manual path to DLL" };

	public ScriptCommand() : base("script", "Generate a schema script from the latest snapshot")
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
			string snapshotPath = Path.Combine(contextProjectDir, "Migrations", "sheetly_snapshot.json");

			if (!File.Exists(snapshotPath))
			{
				Console.WriteLine("⚠️ Snapshot topilmadi. Avval 'migrations add' buyrug'ini ishlating.");
				return;
			}

			var snapshotJson = await File.ReadAllTextAsync(snapshotPath);
			var snapshot = JsonSerializer.Deserialize<MigrationSnapshot>(snapshotJson);

			Console.WriteLine($"--- Sheetly Schema Script (Generated at {DateTime.Now}) ---");
			foreach (var entity in snapshot!.Entities.Values)
			{
				Console.WriteLine($"Sheet: {entity.TableName}");
				foreach (var col in entity.Columns)
				{
					string pk = col.IsPrimaryKey ? "[PK]" : "";
					Console.WriteLine($"  - {col.PropertyName} ({col.DataType}) {pk}");
				}
				Console.WriteLine();
			}
		}
		catch (Exception ex) { Console.WriteLine($"❌ Xato: {ex.Message}"); }
	}
}