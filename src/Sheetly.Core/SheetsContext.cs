using Sheetly.Core.Abstractions;
using Sheetly.Core.Infrastructure;
using Sheetly.Core.Mapping;
using Sheetly.Core.Migration;
using Sheetly.Core.Configuration;
using System.Reflection;

namespace Sheetly.Core;

public abstract class SheetsContext : IDisposable
{
	public ISheetsProvider provider { get; private set; } = default!;
	public DatabaseFacade Database { get; private set; } = default!;

	private readonly Dictionary<Type, object> sets = [];

	protected virtual void OnModelCreating(ModelBuilder modelBuilder) { }

	protected virtual void OnConfiguring(SheetsOptions options) { }

	public virtual async Task InitializeAsync(ISheetsProvider? provider = null)
	{
		if (provider == null)
		{
			var options = new SheetsOptions();
			OnConfiguring(options);
			provider = options.Provider ?? throw new InvalidOperationException("ISheetsProvider ko'rsatilmadi. OnConfiguring-da UseGoogleSheets metodini chaqiring.");
		}

		this.provider = provider;
        Database = new DatabaseFacade(this.provider);

		await this.provider.InitializeAsync();

		var modelBuilder = new ModelBuilder();
		OnModelCreating(modelBuilder);

		var snapshot = MigrationBuilder.BuildFromContext(GetType(), modelBuilder);
		InitializeSets(provider, snapshot);
	}

	private void InitializeSets(ISheetsProvider provider, MigrationSnapshot snapshot)
	{
		var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(SheetsSet<>));

		foreach (var prop in properties)
		{
			var entityType = prop.PropertyType.GetGenericArguments()[0];
			var tableName = EntityMapper.GetTableName(entityType);

			if (snapshot.Entities.TryGetValue(tableName, out var schema))
			{
				var setInstance = Activator.CreateInstance(
					typeof(SheetsSet<>).MakeGenericType(entityType),
					provider,
					schema,
					snapshot.Entities);

				if (setInstance != null)
				{
					prop.SetValue(this, setInstance);
					sets[entityType] = setInstance;
				}
			}
		}
	}

	public async Task<int> SaveChangesAsync()
	{
		int total = 0;
		foreach (var set in sets.Values)
		{
			var method = set.GetType().GetMethod("SaveChangesInternalAsync",
				BindingFlags.NonPublic | BindingFlags.Instance);

			if (method != null)
			{
				var result = await (Task<int>)method.Invoke(set, null)!;
				total += result;
			}
		}
		return total;
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			provider?.Dispose();
		}
	}

	~SheetsContext()
	{
		Dispose(false);
	}
}