using Sheetly.Google;
using Sheetly.Sample;
using Sheetly.Sample.Models;

Console.WriteLine("🚀 Sheetly Professional Test boshlandi...");

string jsonPath = "credentials.json";
string googleSheetId = "1bNZnlJJ81VLbM5VeWoy9uCq4Ynz2bkAXaJlFJAYy_Sc";

try
{
	var context = new MySheetsContext();
	var provider = new GoogleSheetProvider(jsonPath, googleSheetId);

	// CLI yaratgan snapshot yo'li (AbsolutePath yoki Relative)
	string snapshotPath = Path.Combine(Directory.GetCurrentDirectory(), "Migrations/sheetly_snapshot.json");
	var migrationService = new GoogleMigrationService(provider, snapshotPath);

	// Mana shu yerda 'mM7Wij+4fAPrv5jc3ZCE3rBjcm+ScFomvWrtwt+CxU4=' hashi tekshiriladi
	await context.InitializeAsync(provider, migrationService);

	Console.WriteLine("📁 Migratsiya tekshirildi, context tayyor.");
}
catch (Exception ex)
{
	Console.WriteLine($"❌ Xatolik yuz berdi: {ex.Message}");
	if (ex.InnerException != null)
		Console.WriteLine($"🔍 Ichki xato: {ex.InnerException.Message}");
}