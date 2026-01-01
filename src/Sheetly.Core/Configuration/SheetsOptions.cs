namespace Sheetly.DependencyInjection.Options;

public class SheetsOptions
{
	public string? CredentialsPath { get; set; }
	public string? SpreadsheetId { get; set; }
	public string MigrationsFolder { get; set; } = "Migrations";
	public string SnapshotFileName { get; set; } = "sheetly_snapshot.json";

	public string GetFullSnapshotPath()
	{
		return Path.Combine(Directory.GetCurrentDirectory(), MigrationsFolder, SnapshotFileName);
	}
}