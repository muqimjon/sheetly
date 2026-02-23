using Sheetly.Core.Abstractions;

namespace Sheetly.Core.Configuration;

public class SheetsOptions
{
	public string? ConnectionString { get; set; }
	public ISheetsProvider? Provider { get; set; }
	public IMigrationService? MigrationService { get; set; }
	public string? MigrationsAssembly { get; set; }
}