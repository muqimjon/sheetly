using Sheetly.Sample;

Console.WriteLine("🚀 SHEETLY v1.0.0 - ENTITY FRAMEWORK FOR GOOGLE SHEETS");
Console.WriteLine("=" + new string('=', 80));
Console.WriteLine();

try
{
    // Run comprehensive test suite
    await ComprehensiveTests.RunAllTests();
    
    Console.WriteLine("\n✨ Testing complete! Now let's verify data in Google Sheets...");
    Console.WriteLine("\n📊 Please check your Google Sheets and share:");
    Console.WriteLine("   1. Categories sheet data (all rows)");
    Console.WriteLine("   2. Products sheet data (all rows)");
    Console.WriteLine("   3. __SheetlyMigrationsHistory__ sheet");
    Console.WriteLine("   4. __SheetlySchema__ sheet (should be hidden)");
    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Fatal Error: {ex.Message}");
    Console.WriteLine($"   Type: {ex.GetType().Name}");
    if (ex.InnerException != null)
        Console.WriteLine($"   Inner: {ex.InnerException.Message}");
}
