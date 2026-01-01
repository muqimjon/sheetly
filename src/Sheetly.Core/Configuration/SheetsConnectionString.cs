namespace Sheetly.Core.Configuration;

public class SheetsConnectionString
{
    public string Provider { get; private set; } = string.Empty;
    public string CredentialsPath { get; private set; } = string.Empty;
    public string SpreadsheetId { get; private set; } = string.Empty;
    public string MigrationPath { get; private set; } = ".sheetly/migration.json";

    public static SheetsConnectionString Parse(string connectionString)
    {
        var result = new SheetsConnectionString();
        var pairs = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2) continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            switch (key.ToLowerInvariant())
            {
                case "provider":
                    result.Provider = value;
                    break;
                case "credentialspath":
                    result.CredentialsPath = value;
                    break;
                case "spreadsheetid":
                    result.SpreadsheetId = value;
                    break;
                case "migrationpath":
                    result.MigrationPath = value;
                    break;
            }
        }

        return result;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Provider))
            throw new InvalidOperationException("Provider is required in connection string");

        if (string.IsNullOrWhiteSpace(CredentialsPath))
            throw new InvalidOperationException("CredentialsPath is required in connection string");

        if (string.IsNullOrWhiteSpace(SpreadsheetId))
            throw new InvalidOperationException("SpreadsheetId is required in connection string");
    }
}