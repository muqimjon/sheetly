namespace Sheetly.Core.Configuration;

public class SheetsConnectionString
{
	public string Provider { get; private set; } = "GoogleSheets";
	public string CredentialsPath { get; private set; } = string.Empty;
	public string SpreadsheetId { get; private set; } = string.Empty;
	public string MigrationPath { get; private set; } = "Migrations";

	public static SheetsConnectionString Parse(string connectionString)
	{
		var result = new SheetsConnectionString();
		if (string.IsNullOrWhiteSpace(connectionString)) return result;

		var pairs = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);

		foreach (var pair in pairs)
		{
			var parts = pair.Split('=', 2);
			if (parts.Length != 2) continue;

			var key = parts[0].Trim().ToLowerInvariant();
			var value = parts[1].Trim();

			switch (key)
			{
				case "provider":
					result.Provider = value;
					break;
				case "credentialspath":
				case "json_path":
				case "credentials":
					result.CredentialsPath = value;
					break;
				case "spreadsheetid":
				case "spreadsheet_id":
				case "id":
					result.SpreadsheetId = value;
					break;
				case "migrationpath":
				case "migrations":
					result.MigrationPath = value;
					break;
			}
		}

		if (string.IsNullOrWhiteSpace(result.Provider) || result.Provider == "GoogleSheets")
		{
			if (!string.IsNullOrWhiteSpace(result.CredentialsPath) || !string.IsNullOrWhiteSpace(result.SpreadsheetId))
			{
				result.Provider = "GoogleSheets";
			}
		}

		return result;
	}

	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(Provider))
			throw new InvalidOperationException("Provider is required in connection string");

		if (string.IsNullOrWhiteSpace(CredentialsPath))
			throw new InvalidOperationException("CredentialsPath (JSON_PATH) is required in connection string");

		if (string.IsNullOrWhiteSpace(SpreadsheetId))
			throw new InvalidOperationException("SpreadsheetId is required in connection string");
	}
}