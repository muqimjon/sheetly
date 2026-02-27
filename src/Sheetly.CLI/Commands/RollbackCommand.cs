using Sheetly.CLI.Helpers;
using System.CommandLine;
using System.Reflection;

namespace Sheetly.CLI.Commands;

/// <summary>
/// Rolls back the last applied migration.
/// </summary>
public class RollbackCommand : Command
{
	private readonly Option<string?> _projectOption = new("--project", ["-p"]) { Description = "Path to project" };
	private readonly Option<bool> _noBuildOption = new("--no-build", ["-n"]) { Description = "Do not build project" };

	public RollbackCommand() : base("rollback", "Rollback the last migration")
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
			string migrationsDir = Path.Combine(contextProjectDir, "Migrations");

			// Find last C# migration
			var migrations = Directory.GetFiles(migrationsDir, "*.cs")
				.Where(f => !f.EndsWith(".Designer.cs"))
				.OrderByDescending(f => f)
				.ToList();

			if (migrations.Count == 0)
			{
				Console.WriteLine("⚠️ No migrations to rollback.");
				return;
			}

			var lastMigration = migrations[0];
			var migrationFileName = Path.GetFileName(lastMigration);

			Console.Write($"⚠️ Are you sure you want to rollback '{migrationFileName}'? (y/N): ");
			var response = Console.ReadLine();

			if (!string.Equals(response, "y", StringComparison.OrdinalIgnoreCase))
			{
				Console.WriteLine("Cancelled.");
				return;
			}

			// Delete the migration file
			File.Delete(lastMigration);
			Console.WriteLine($"✅ Deleted: {migrationFileName}");

			// If there are previous migrations, restore snapshot from them
			if (migrations.Count > 1)
			{
				Console.WriteLine("💡 Run 'dotnet-sheetly migrations add' again to regenerate snapshot from current model.");
			}
			else
			{
				// Delete ModelSnapshot if no more migrations
				var snapshotFiles = Directory.GetFiles(migrationsDir, "*ModelSnapshot.cs");
				foreach (var sf in snapshotFiles)
				{
					File.Delete(sf);
					Console.WriteLine($"🗑️ Deleted snapshot: {Path.GetFileName(sf)}");
				}
			}

			Console.WriteLine("✅ Rollback complete.");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Error: {ex.Message}");
		}
	}
}
