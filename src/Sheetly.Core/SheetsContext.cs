using System.Reflection;
using Sheetly.Core.Abstractions;
using Sheetly.Core.Mapping;
using Sheetly.Core.Migration;

namespace Sheetly.Core;

public abstract class SheetsContext
{
	private readonly Dictionary<Type, object> _sets = [];

	protected virtual void OnModelCreating(ModelBuilder modelBuilder) { }

	public async Task InitializeAsync(ISheetProvider provider, IMigrationService migrationService)
	{
		var modelBuilder = new ModelBuilder();
		OnModelCreating(modelBuilder);

		var currentSnapshot = MigrationBuilder.BuildFromContext(GetType(), modelBuilder);
		var existingSnapshot = await migrationService.LoadSnapshotAsync();

		if (!string.IsNullOrEmpty(existingSnapshot.ModelHash) &&
			existingSnapshot.ModelHash != currentSnapshot.ModelHash)
		{
			throw new InvalidOperationException(
				"Model o'zgargan! Iltimos, migratsiyani yangilang (Snapshotni qayta yarating). " +
				"Hozirgi model Snapshot faylidagi modelga mos kelmayapti.");
		}

		await migrationService.ApplyMigrationAsync(currentSnapshot);
		await migrationService.SaveSnapshotAsync(currentSnapshot);

		InitializeSets(provider, currentSnapshot);
	}

	private void InitializeSets(ISheetProvider provider, MigrationSnapshot snapshot)
	{
		var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(SheetsSet<>));

		foreach (var prop in properties)
		{
			var entityType = prop.PropertyType.GetGenericArguments()[0];
			var tableName = EntityMapper.GetTableName(entityType);

			if (snapshot.Entities.TryGetValue(tableName, out var schema))
			{
				var setInstance = Activator.CreateInstance(typeof(SheetsSet<>).MakeGenericType(entityType), provider, schema);
				prop.SetValue(this, setInstance);
				_sets[entityType] = setInstance!;
			}
		}
	}

	public async Task<int> SaveChangesAsync()
	{
		int total = 0;
		foreach (var set in _sets.Values)
		{
			var method = set.GetType().GetMethod("SaveChangesInternalAsync", BindingFlags.NonPublic | BindingFlags.Instance);
			if (method != null) total += await (Task<int>)method.Invoke(set, null)!;
		}
		return total;
	}
}