using Sheetly.Core;
using Sheetly.Core.Configuration;

namespace Sheetly.Google;

public static class GoogleSheetsFactory
{
	public static async Task<T> CreateContextAsync<T>(string connectionString) where T : SheetsContext, new()
	{
		var connString = SheetsConnectionString.Parse(connectionString);
		connString.Validate();

		var provider = new GoogleSheetProvider(connString.CredentialsPath, connString.SpreadsheetId);
		var context = new T();
		await context.InitializeAsync(provider);

		return context;
	}

	public static async Task<T> CreateContextAsync<T>(
		string credentialsPath,
		string spreadsheetId
	) where T : SheetsContext, new()
	{
		var connectionString = $"Provider=GoogleSheets;CredentialsPath={credentialsPath};SpreadsheetId={spreadsheetId}";
		return await CreateContextAsync<T>(connectionString);
	}
}