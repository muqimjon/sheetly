using Sheetly.Core.Configuration;

namespace Sheetly.Excel;

public static class ExcelSheetsOptionsExtensions
{
	/// <summary>
	/// Configures Sheetly to use a local Excel (.xlsx) file as the backing store.
	/// </summary>
	public static SheetsOptions UseExcel(this SheetsOptions options, string filePath)
	{
		options.ConnectionString = $"Provider=Excel;FilePath={filePath}";
		var provider = new ExcelSheetProvider(filePath);
		options.Provider = provider;
		options.MigrationService = new ExcelMigrationService(provider);
		return options;
	}
}
