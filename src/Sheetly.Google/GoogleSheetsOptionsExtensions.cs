using Sheetly.Core.Configuration;

namespace Sheetly.Google;

public static class GoogleSheetsOptionsExtensions
{
	public static SheetsOptions UseGoogleSheets(this SheetsOptions options, string connectionString)
	{
		options.ConnectionString = connectionString;
		var conn = SheetsConnectionString.Parse(connectionString);
		var provider = new GoogleSheetProvider(conn.SpreadsheetId, conn.CredentialsPath);
		options.Provider = provider;
		options.MigrationService = new GoogleMigrationService(provider);
		return options;
	}

	public static SheetsOptions UseGoogleSheets(this SheetsOptions options, string spreadsheetId, string credentialsPath)
	{
		options.ConnectionString = $"Provider=GoogleSheets;CredentialsPath={credentialsPath};SpreadsheetId={spreadsheetId}";
		var provider = new GoogleSheetProvider(spreadsheetId, credentialsPath);
		options.Provider = provider;
		options.MigrationService = new GoogleMigrationService(provider);
		return options;
	}
}
