using Sheetly.Core.Abstractions;
using Sheetly.Core.Diagnostics;

namespace Sheetly.Core.Configuration;

public class SheetsOptions
{
	public string? ConnectionString { get; set; }
	public ISheetsProvider? Provider { get; set; }
	public IMigrationService? MigrationService { get; set; }
	public string? MigrationsAssembly { get; set; }
	public SheetlyLogger? Logger { get; private set; }

	/// <summary>
	/// Routes Sheetly's log messages to <paramref name="sink"/> (e.g. <c>Console.WriteLine</c>),
	/// mirroring EF Core's <c>LogTo</c>. Attaches to the provider/migration service too when they
	/// support logging.
	/// </summary>
	public SheetsOptions LogTo(Action<string> sink, SheetlyLogLevel minimumLevel = SheetlyLogLevel.Information)
	{
		Logger = new SheetlyLogger(sink, minimumLevel);
		if (Provider is ISupportsLogging provider) provider.SetLogger(Logger);
		if (MigrationService is ISupportsLogging migrationService) migrationService.SetLogger(Logger);
		return this;
	}
}