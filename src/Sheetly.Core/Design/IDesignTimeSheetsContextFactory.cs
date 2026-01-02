namespace Sheetly.Core.Design;

public interface IDesignTimeSheetsContextFactory<out TContext> where TContext : SheetsContext
{
    TContext CreateDbContext(string[] args);
}
