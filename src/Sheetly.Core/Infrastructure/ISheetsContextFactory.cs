namespace Sheetly.Core.Infrastructure;

/// <summary>
/// Creates fully-initialized <typeparamref name="TContext"/> instances on demand, mirroring
/// EF Core's <c>IDbContextFactory</c>. Prefer this over an injected scoped context when you
/// need real async initialization or short-lived contexts (background jobs, parallel work).
/// </summary>
public interface ISheetsContextFactory<TContext> where TContext : SheetsContext
{
	Task<TContext> CreateContextAsync(CancellationToken cancellationToken = default);
}
