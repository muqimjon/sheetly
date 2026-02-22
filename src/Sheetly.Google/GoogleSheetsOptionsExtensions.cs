using Sheetly.Core.Configuration;

namespace Sheetly.Google;

public static class GoogleSheetsOptionsExtensions
{
	public static SheetsOptions UseGoogleSheets(this SheetsOptions options, string connectionString)
	{
		options.ConnectionString = connectionString;
		var conn = SheetsConnectionString.Parse(connectionString);
		var provider = new GoogleSheetProvider(conn.CredentialsPath, conn.SpreadsheetId);
		options.Provider = provider;
		options.MigrationService = new GoogleMigrationService(provider);
		return options;
	}

	public static SheetsOptions UseGoogleSheets(this SheetsOptions options, string credentialsPath, string spreadsheetId)
	{
		var provider = new GoogleSheetProvider(credentialsPath, spreadsheetId);
		options.Provider = provider;
		options.MigrationService = new GoogleMigrationService(provider);
		return options;
	}
}
