using Sheetly.Core.Abstractions;

namespace Sheetly.Core.Infrastructure;

public class DatabaseFacade
{
	private readonly ISheetsProvider _provider;

	public DatabaseFacade(ISheetsProvider provider)
	{
		_provider = provider;
	}

	public async Task DropDatabaseAsync()
	{
		await _provider.DropDatabaseAsync();
	}
}