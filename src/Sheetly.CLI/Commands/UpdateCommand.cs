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
			var (assembly, loadContext) = CliHelper.LoadAssemblyIsolated(dllPath);
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

			// Cross-context: IsSubclassOf(typeof(Migration)) fails — use string-based check
			var migrationTypes = assembly.GetTypes()
				.Where(t => CliHelper.IsSubclassOf(t, "Sheetly.Core.Migration.Migration") && !t.IsAbstract)
				.Select(t => new { Type = t, MigrationId = CliHelper.GetMigrationAttributeId(t) })
				.Where(x => x.MigrationId != null)
				.OrderBy(x => x.MigrationId)
				.ToList();

			if (migrationTypes.Count == 0)
			{
				Console.WriteLine("⚠️ No migrations found in the project.");
				return;
			}

			var pendingMigrations = migrationTypes
				.Where(x => !appliedMigrations.Contains(x.MigrationId!))
				.ToList();

			if (pendingMigrations.Count == 0)
			{
				Console.WriteLine("✅ Database is up to date.");
				return;
			}

			Console.WriteLine($"🚀 Found {pendingMigrations.Count} pending migration(s).");

			// Cross-context: instantiate snapshot and bridge via JSON
			var snapshotType = assembly.GetTypes()
				.FirstOrDefault(t => t.Name.EndsWith("ModelSnapshot") &&
								CliHelper.IsSubclassOf(t, "Sheetly.Core.Migrations.MigrationSnapshot"));
			MigrationSnapshot? currentSnapshot = null;
			if (snapshotType != null)
			{
				var isolatedSnap = Activator.CreateInstance(snapshotType);
				currentSnapshot = CliHelper.BridgeFromJson<MigrationSnapshot>(isolatedSnap);
			}

			// Load isolated MigrationBuilder once for all pending migrations
			var coreAsm = CliHelper.GetCoreAssembly(assembly, loadContext);
			var isolatedMbType = coreAsm.GetType("Sheetly.Core.Migrations.MigrationBuilder")!;

			foreach (var pm in pendingMigrations)
			{
				var migrationId = pm.MigrationId!;
				Console.Write($"Applying {migrationId}... ");

				// Cross-context: invoke Up() with isolated MigrationBuilder, then bridge operations via JSON
				var migrationObj = Activator.CreateInstance(pm.Type)!;
				var builder = Activator.CreateInstance(isolatedMbType)!;
				pm.Type.GetMethod("Up")!.Invoke(migrationObj, [builder]);
				var isolatedOps = isolatedMbType.GetMethod("GetOperations")!.Invoke(builder, null)!;
				var operations = CliHelper.BridgeMigrationOperations(isolatedOps);

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
