using System.CommandLine;
using System.Reflection;
using System.Text.Json;
using Sheetly.Core;
using Sheetly.Core.Migration;
using Sheetly.Google;
using Sheetly.CLI.Helpers;

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
			var assembly = Assembly.LoadFrom(Path.GetFullPath(dllPath));
			var contextType = assembly.GetExportedTypes().FirstOrDefault(t => CliHelper.IsSubclassOfSheetsContext(t))
				?? throw new Exception("SheetsContext topilmadi.");

			string contextProjectDir = CliHelper.FindProjectRootFromDll(contextType.Assembly.Location);
			string? connStr = CliHelper.GetConnectionString(contextProjectDir) ?? throw new Exception("ConnectionString topilmadi.");

			var factoryType = assembly.GetExportedTypes().FirstOrDefault(t => t.Name == "GoogleSheetsFactory")
							  ?? typeof(GoogleSheetsFactory);

			var method = factoryType.GetMethods(BindingFlags.Public | BindingFlags.Static)
				.FirstOrDefault(m => m.Name == "CreateContextAsync" && m.IsGenericMethod && m.GetParameters().Length == 1)
				?.MakeGenericMethod(contextType);

			if (method == null) throw new Exception("CreateContextAsync topilmadi.");

			Console.WriteLine("⏳ Google Sheets-ga ulanmoqda...");
			var task = (Task)method.Invoke(null, [connStr])!;
			await task;

			var context = (SheetsContext)((dynamic)task).Result;

			if (context.provider == null) throw new Exception("Provider is required in connection string.");

			string snapshotPath = Path.Combine(contextProjectDir, "Migrations", "sheetly_snapshot.json");
			if (!File.Exists(snapshotPath)) throw new Exception("Snapshot topilmadi. Avval 'migrations add' buyrug'ini ishlating.");

			var snapshotJson = await File.ReadAllTextAsync(snapshotPath, ct);
			var snapshot = JsonSerializer.Deserialize<MigrationSnapshot>(snapshotJson)!;

			Console.WriteLine("🔄 Migratsiyalar Sheets-ga qo'llanilmoqda...");
			await context.Database.ApplyMigrationAsync(snapshot);

			var migrationServiceType = assembly.GetExportedTypes().FirstOrDefault(t => t.Name == "GoogleMigrationService") ?? typeof(GoogleMigrationService);
			var migrationService = Activator.CreateInstance(migrationServiceType, context.provider, snapshotPath);
			var updateHistoryMethod = migrationService.GetType().GetMethod("UpdateHistoryAsync");

			if (updateHistoryMethod != null)
				await (Task)updateHistoryMethod.Invoke(migrationService, [DateTime.Now.ToString("yyyyMMddHHmmss"), snapshotJson])!;

			Console.WriteLine("✅ Muvaffaqiyatli yakunlandi.");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Xato: {ex.Message}");
			if (ex.InnerException != null) Console.WriteLine($"🔍 Tafsilot: {ex.InnerException.Message}");
		}
	}
}