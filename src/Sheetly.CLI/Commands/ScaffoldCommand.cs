using Sheetly.CLI.Helpers;
using Sheetly.Core.Migration;
using System.CommandLine;
using System.Reflection;
using System.Text.Json;

namespace Sheetly.CLI.Commands;

public class ScaffoldCommand : Command
{
	private readonly Option<string?> _projectOption = new("--project", ["-p"]);
	private readonly Option<string?> _outputDirOption = new("--output-dir", ["-o"]);

	public ScaffoldCommand() : base("scaffold", "Scaffold model classes from Google Sheets")
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
			var (assembly, loadContext) = CliHelper.LoadAssemblyIsolated(dllPath);
			var contextType = assembly.GetExportedTypes().FirstOrDefault(t => CliHelper.IsSubclassOfSheetsContext(t))
				?? throw new Exception("SheetsContext not found.");

			string contextProjectDir = CliHelper.FindProjectRootFromDll(dllPath);
			string? connStr = CliHelper.GetConnectionString(contextProjectDir)
				?? CliHelper.GetConnectionStringFromContext(contextType);

			// Use isolated Sheetly.Google factory so contextType satisfies its T : SheetsContext constraint
			var googleAsm = CliHelper.GetGoogleAssembly(assembly, loadContext)
				?? throw new Exception("Sheetly.Google not found in project references.");
			var factoryType = googleAsm.GetType("Sheetly.Google.GoogleSheetsFactory")!;
			var method = factoryType.GetMethods().First(m => m.Name == "CreateContextAsync").MakeGenericMethod(contextType);
			var task = (Task)method.Invoke(null, [connStr])!;
			await task;
			dynamic context = ((dynamic)task).Result;

			// Provider.GetAllRowsAsync returns Task<List<IList<object>>> — BCL types survive cross-context cast
			var providerProp = contextType.BaseType!.GetProperty("Provider")!;
			var provider = providerProp.GetValue(context);
			var getRowsTask = (Task)provider!.GetType()
				.GetMethod("GetAllRowsAsync", new[] { typeof(string) })!
				.Invoke(provider, new object[] { "__SheetlyHistory__" })!;
			await getRowsTask;
			var rows = (List<IList<object>>)((dynamic)getRowsTask).Result;
			if (rows.Count <= 1) throw new Exception("Migration history not found.");

			var snapshotJson = rows.Last()[2].ToString()!;
			var snapshot = JsonSerializer.Deserialize<MigrationSnapshot>(snapshotJson)!;

			string finalPath = Path.Combine(contextProjectDir, outputDir ?? "Models/Scaffolded");
			Directory.CreateDirectory(finalPath);

			foreach (var entity in snapshot.Entities.Values)
			{
				var code = CliHelper.GenerateClassCode(entity);
				await File.WriteAllTextAsync(Path.Combine(finalPath, $"{entity.ClassName}.cs"), code, ct);
				Console.WriteLine($"📄 Created: {entity.ClassName}.cs");
			}
			Console.WriteLine("✅ Scaffolding complete.");
		}
		catch (Exception ex) { Console.WriteLine($"❌ Error: {ex.Message}"); }
	}
}