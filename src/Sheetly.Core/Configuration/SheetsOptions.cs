using Sheetly.Core.Abstractions;

namespace Sheetly.Core.Configuration;

public class SheetsOptions
{
	public string? ConnectionString { get; set; }
	public ISheetsProvider? Provider { get; set; }
	public string MigrationsFolder { get; set; } = "Migrations";
	public string SnapshotFileName { get; set; } = "sheetly_snapshot.json";
	public string? MigrationsAssembly { get; set; }

	public string GetFullSnapshotPath(string? projectRoot = null)
	{
		var root = projectRoot ?? Directory.GetCurrentDirectory();
		return Path.Combine(root, MigrationsFolder, SnapshotFileName);
	}
}