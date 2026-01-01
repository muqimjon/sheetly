namespace Sheetly.DependencyInjection.Options;

public class SheetsOptionsBuilder
{
	private readonly SheetsOptions _options = new();

	public SheetsOptionsBuilder UseGoogleSheets(string credentialsPath, string spreadsheetId)
	{
		_options.CredentialsPath = credentialsPath;
		_options.SpreadsheetId = spreadsheetId;
		return this;
	}

	public SheetsOptionsBuilder SetMigrationsFolder(string folder)
	{
		_options.MigrationsFolder = folder;
		return this;
	}

	public SheetsOptions Build() => _options;
}