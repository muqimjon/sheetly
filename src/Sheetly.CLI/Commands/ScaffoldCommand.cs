using Sheetly.CLI.Helpers;
using Sheetly.Core;
using Sheetly.Core.Migration;
using Sheetly.Google;
using System.CommandLine;
using System.Reflection;
using System.Text.Json;

namespace Sheetly.CLI.Commands;

public class ScaffoldCommand : Command
{
	private readonly Option<string?> _projectOption = new("--project", ["-p"]);
	private readonly Option<string?> _outputDirOption = new("--output-dir", ["-o"]);

	public ScaffoldCommand() : base("scaffold", "Google Sheets'dan model klaslarini qayta yaratish")
	{
		this.Add(_projectOption);
		this.Add(_outputDirOption);
		this.SetAction(async (parseResult, ct) => await ExecuteAsync(
			parseResult.GetValue(_projectOption),
			parseResult.GetValue(_outputDirOption),
			ct));
	}

	private async Task ExecuteAsync(string? projectPath, string? outputDir, CancellationToken ct)
	{
		string dllPath = CliHelper.FindProjectDll(true, projectPath);
		if (string.IsNullOrEmpty(dllPath)) return;

		try
		{
			var assembly = Assembly.LoadFrom(Path.GetFullPath(dllPath));
			var contextType = assembly.GetExportedTypes().FirstOrDefault(t => CliHelper.IsSubclassOfSheetsContext(t))
				?? throw new Exception("SheetsContext topilmadi.");

			string contextProjectDir = CliHelper.FindProjectRootFromDll(dllPath);
			string? connStr = CliHelper.GetConnectionString(contextProjectDir);

			var method = typeof(GoogleSheetsFactory).GetMethods().First(m => m.Name == "CreateContextAsync").MakeGenericMethod(contextType);
			var task = (Task)method.Invoke(null, [connStr])!;
			await task;
			var context = (SheetsContext)((dynamic)task).Result;

			var rows = await context.provider.GetAllRowsAsync("__SheetlyHistory__");
			if (rows.Count <= 1) throw new Exception("Migratsiya tarixi topilmadi.");

			var snapshotJson = rows.Last()[2].ToString()!;
			var snapshot = JsonSerializer.Deserialize<MigrationSnapshot>(snapshotJson)!;

			string finalPath = Path.Combine(contextProjectDir, outputDir ?? "Models/Scaffolded");
			Directory.CreateDirectory(finalPath);

			foreach (var entity in snapshot.Entities.Values)
			{
				var code = CliHelper.GenerateClassCode(entity);
				await File.WriteAllTextAsync(Path.Combine(finalPath, $"{entity.ClassName}.cs"), code, ct);
				Console.WriteLine($"📄 Yaratildi: {entity.ClassName}.cs");
			}
			Console.WriteLine("✅ Scaffolding tugadi.");
		}
		catch (Exception ex) { Console.WriteLine($"❌ Xato: {ex.Message}"); }
	}
}