using Sheetly.CLI.Helpers;
using Sheetly.Core.Configuration;
using Sheetly.Core.Migration;
using Sheetly.Core.Migrations;
using Sheetly.Core.Migrations.Operations;
using Sheetly.Google;
using System.CommandLine;
using System.Reflection;

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
			?? throw new Exception("SheetsContext not found.");

			string contextProjectDir = CliHelper.FindProjectRootFromDll(contextType.Assembly.Location);
			string? connStr = CliHelper.GetConnectionString(contextProjectDir)
				?? CliHelper.GetConnectionStringFromContext(contextType)
				?? throw new Exception("ConnectionString not found. Configure OnConfiguring() or add appsettings.json.");

			// Create provider directly — bypassing full context init so migration checks don't run
			Console.WriteLine("⏳ Connecting to Google Sheets...");
			var connString = SheetsConnectionString.Parse(connStr);
			connString.Validate();
			var provider = new GoogleSheetProvider(connString.CredentialsPath, connString.SpreadsheetId);
			await provider.InitializeAsync();

			var migrationService = new GoogleMigrationService(provider);

			var appliedMigrations = await migrationService.GetAppliedMigrationsAsync();

			var migrationTypes = assembly.GetTypes()
			.Where(t => t.IsSubclassOf(typeof(Migration)) && !t.IsAbstract)
			.Select(t => new { Type = t, Attribute = t.GetCustomAttribute<MigrationAttribute>() })
			.Where(x => x.Attribute != null)
			.OrderBy(x => x.Attribute!.Id)
			.ToList();

			if (migrationTypes.Count == 0)
			{
				Console.WriteLine("⚠️ No migrations found in the project.");
				return;
			}

			var pendingMigrations = migrationTypes
			.Where(x => !appliedMigrations.Contains(x.Attribute!.Id))
			.ToList();

			if (pendingMigrations.Count == 0)
			{
				Console.WriteLine("✅ Database is up to date.");
				return;
			}

			Console.WriteLine($"🚀 Found {pendingMigrations.Count} pending migration(s).");

			// Load snapshot to enrich operations with ClassName / IsAutoIncrement
			var snapshotType = assembly.GetTypes()
			.FirstOrDefault(t => t.Name.EndsWith("ModelSnapshot") && t.IsSubclassOf(typeof(MigrationSnapshot)));
			MigrationSnapshot? currentSnapshot = snapshotType != null
			? (MigrationSnapshot?)Activator.CreateInstance(snapshotType)
			: null;

			foreach (var pm in pendingMigrations)
			{
				var migrationId = pm.Attribute!.Id;
				Console.Write($"Applying {migrationId}... ");

				var migration = (Migration)Activator.CreateInstance(pm.Type)!;
				var builder = new Core.Migrations.MigrationBuilder();
				migration.Up(builder);
				var operations = builder.GetOperations();

				if (currentSnapshot != null)
				{
					foreach (var op in operations.OfType<CreateTableOperation>())
					{
						if (!currentSnapshot.Entities.TryGetValue(op.Name, out var entity)) continue;
						op.ClassName = entity.ClassName;
						foreach (var col in op.Columns)
						{
							var sc = entity.Columns.FirstOrDefault(c => c.Name == col.Name);
							if (sc == null) continue;
							col.IsAutoIncrement = sc.IsAutoIncrement;
							if (sc.IsPrimaryKey) col.IsUnique = true;
						}
					}

					foreach (var op in operations.OfType<AddColumnOperation>())
					{
						if (!currentSnapshot.Entities.TryGetValue(op.Table, out var entity)) continue;
						var sc = entity.Columns.FirstOrDefault(c => c.Name == op.Name);
						if (sc == null) continue;
						op.IsAutoIncrement = sc.IsAutoIncrement;
						op.ClassName = entity.ClassName;
						if (sc.IsPrimaryKey) op.IsUnique = true;
					}
				}

				await migrationService.ApplyMigrationAsync(operations, migrationId);
				Console.WriteLine("Done.");
			}

			Console.WriteLine("✅ All migrations applied successfully.");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Error: {ex.Message}");
			if (ex.InnerException != null) Console.WriteLine($"🔍 Detail: {ex.InnerException.Message}");
		}
	}
}
