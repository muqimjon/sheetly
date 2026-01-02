using Sheetly.Core.Configuration;

namespace Sheetly.Google;

public static class GoogleSheetsOptionsExtensions
{
    public static SheetsOptions UseGoogleSheets(this SheetsOptions options, string connectionString)
    {
        options.ConnectionString = connectionString;
        var conn = SheetsConnectionString.Parse(connectionString);
        options.Provider = new GoogleSheetProvider(conn.CredentialsPath, conn.SpreadsheetId);
        return options;
    }

    public static SheetsOptions UseGoogleSheets(this SheetsOptions options, string credentialsPath, string spreadsheetId)
    {
        options.Provider = new GoogleSheetProvider(credentialsPath, spreadsheetId);
        return options;
    }
}
