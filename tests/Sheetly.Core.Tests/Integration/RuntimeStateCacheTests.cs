using Sheetly.Core.Abstractions;
using Sheetly.Core.Configuration;
using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// C10 — once a (context type + connection) has passed startup verification, subsequent contexts
/// skip the remote re-check. This exercises that cache-hit path still initializes correctly.
/// </summary>
public class RuntimeStateCacheTests
{
	private sealed class CachedContext(ISheetsProvider provider) : TestDbContext
	{
		protected override void OnConfiguring(SheetsOptions options)
		{
			options.Provider = provider;
			options.ConnectionString = "mem://c10-cache-test";
		}
	}

	[Fact]
	public async Task SecondContextWithSameConnection_InitializesAndSharesData()
	{
		var (_, provider) = await TestContextFactory.CreateAsync();

		var first = new CachedContext(provider);
		await first.InitializeAsync();          // cache miss → verifies, then marks verified

		var second = new CachedContext(provider);
		await second.InitializeAsync();         // cache hit → skips the remote re-check

		first.Categories.Add(new Category { Name = "Cached" });
		await first.SaveChangesAsync();

		var seen = await second.Categories.ToListAsync();
		Assert.Contains(seen, c => c.Name == "Cached");
	}
}
