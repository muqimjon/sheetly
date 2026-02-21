using System.CommandLine;

namespace Sheetly.CLI.Commands;

/// <summary>
/// Lists all migrations and their status.
/// </summary>
public class ListCommand : Command
{
	private readonly Option<string?> _projectOption = new("--project", ["-p"]) { Description = "Path to project" };
	private readonly Option<bool> _noBuildOption = new("--no-build", ["-n"]) { Description = "Do not build project" };

	public ListCommand() : base("list", "List all migrations")
	{
		this.Add(_projectOption);
		this.Add(_noBuildOption);

		this.SetAction(async (parseResult, ct) =>
		{
			string? projectPath = parseResult.GetValue(_projectOption);
			await ExecuteAsync(projectPath, ct);
		});
	}

	private async Task ExecuteAsync(string? projectPath, CancellationToken ct)
	{
		try
		{
			string migrationsDir = FindMigrationsDirectory(projectPath);

			if (!Directory.Exists(migrationsDir))
			{
				Console.WriteLine("⚠️ No migrations directory found.");
				return;
			}

			// List C# migration files (exclude ModelSnapshot)
			var migrations = Directory.GetFiles(migrationsDir, "*.cs")
				.Where(f => !f.Contains("ModelSnapshot"))
				.OrderBy(f => f)
				.ToList();

			Console.WriteLine("📋 Migrations:");
			Console.WriteLine();

			if (migrations.Count == 0)
			{
				Console.WriteLine("   No migrations found.");
				return;
			}

			foreach (var file in migrations)
			{
				var fileName = Path.GetFileNameWithoutExtension(file);
				Console.WriteLine($"   ✓ {fileName}");
			}

			Console.WriteLine();
			Console.WriteLine($"Total: {migrations.Count} migration(s)");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Error: {ex.Message}");
		}

		await Task.CompletedTask;
	}

	private static string FindMigrationsDirectory(string? projectPath)
	{
		var baseDir = projectPath ?? Directory.GetCurrentDirectory();
		return Path.Combine(baseDir, "Migrations");
	}
}
