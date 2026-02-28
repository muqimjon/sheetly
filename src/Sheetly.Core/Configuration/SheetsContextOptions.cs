namespace Sheetly.Core.Configuration;

/// <summary>
/// Typed options for a specific <typeparamref name="TContext"/> instance.
/// Mirrors EF Core's <c>DbContextOptions&lt;TContext&gt;</c> pattern, enabling
/// constructor-based dependency injection:
/// <code>
/// public class AppContext : SheetsContext
/// {
///     public AppContext(SheetsContextOptions&lt;AppContext&gt; options) : base(options) { }
/// }
/// </code>
/// </summary>
public class SheetsContextOptions<TContext> : SheetsOptions where TContext : class
{
}
