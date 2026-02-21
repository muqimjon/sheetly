using Sheetly.Core.Abstractions;
using Sheetly.Core.Configuration;
using Sheetly.Core.Infrastructure;
using Sheetly.Core.Mapping;
using Sheetly.Core.Migration;
using Sheetly.Core.Validation;
using Sheetly.Core.Validation.Rules;
using System.Reflection;

namespace Sheetly.Core;

public abstract class SheetsContext : IDisposable
{
	public ISheetsProvider provider { get; private set; } = default!;
	public DatabaseFacade Database { get; private set; } = default!;

	private readonly Dictionary<Type, object> sets = [];
	private MigrationSnapshot? _currentSnapshot;
	private ConstraintValidator? _validator;

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

		_currentSnapshot = MigrationBuilder.BuildFromContext(GetType(), modelBuilder);
		_validator = new ConstraintValidator(_currentSnapshot);

		// Check migration synchronization
		await CheckMigrationSyncAsync();

		InitializeSets(provider, _currentSnapshot);
	}

	/// <summary>
	/// Checks if local and remote migrations are synchronized.
	/// Warns if there are pending migrations that haven't been applied to the database.
	/// </summary>
	private async Task CheckMigrationSyncAsync()
	{
		try
		{
			// Get local migrations from Migrations folder
			var contextAssembly = GetType().Assembly;
			var localMigrations = GetLocalMigrations(contextAssembly);

			if (localMigrations.Count == 0)
			{
				// No migrations found locally - this is OK for new projects
				return;
			}

			// Get applied migrations from remote database
			var appliedMigrations = await GetAppliedMigrationsFromRemoteAsync();

			// Find pending migrations
			var pendingMigrations = localMigrations
				.Where(m => !appliedMigrations.Contains(m))
				.ToList();

			if (pendingMigrations.Count > 0)
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine("⚠️  WARNING: Pending migrations detected!");
				Console.WriteLine($"   {pendingMigrations.Count} migration(s) have not been applied to the database:");
				foreach (var migration in pendingMigrations.Take(5))
				{
					Console.WriteLine($"   - {migration}");
				}
				if (pendingMigrations.Count > 5)
				{
					Console.WriteLine($"   ... and {pendingMigrations.Count - 5} more");
				}
				Console.WriteLine("   Run 'dotnet sheetly database update' to apply pending migrations.");
				Console.ResetColor();
			}
			else
			{
				// All migrations are applied
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine($"✓ Database is up to date ({appliedMigrations.Count} migrations applied)");
				Console.ResetColor();
			}
		}
		catch (Exception ex)
		{
			// Don't fail initialization if migration check fails
			Console.ForegroundColor = ConsoleColor.DarkYellow;
			Console.WriteLine($"⚠️  Could not verify migration status: {ex.Message}");
			Console.ResetColor();
		}
	}

	/// <summary>
	/// Gets all migration IDs from local migration files.
	/// </summary>
	private List<string> GetLocalMigrations(Assembly assembly)
	{
		var migrations = new List<string>();
		var migrationTypes = assembly.GetTypes()
			.Where(t => t.IsSubclassOf(typeof(Migrations.Migration)) && !t.IsAbstract);

		foreach (var migrationType in migrationTypes)
		{
			var migrationAttr = migrationType.GetCustomAttribute<Migrations.MigrationAttribute>();
			if (migrationAttr != null)
			{
				migrations.Add(migrationAttr.Id);
			}
		}

		return migrations.OrderBy(m => m).ToList();
	}

	/// <summary>
	/// Gets applied migration IDs from the remote database (__SheetlyMigrationsHistory__).
	/// </summary>
	private async Task<List<string>> GetAppliedMigrationsFromRemoteAsync()
	{
		const string HistoryTable = "__SheetlyMigrationsHistory__";

		if (!await provider.SheetExistsAsync(HistoryTable))
		{
			// History table doesn't exist - database is new
			return new List<string>();
		}

		var rows = await provider.GetAllRowsAsync(HistoryTable);

		// Skip header row (index 0), get MigrationId from first column
		return rows.Skip(1)
			.Where(r => r.Count > 0 && !string.IsNullOrEmpty(r[0]?.ToString()))
			.Select(r => r[0]!.ToString()!)
			.OrderBy(m => m)
			.ToList();
	}

	private void InitializeSets(ISheetsProvider provider, MigrationSnapshot snapshot)
	{
		var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(SheetsSet<>));

		foreach (var prop in properties)
		{
			var entityType = prop.PropertyType.GetGenericArguments()[0];

			// Try to find schema by entity type name matching any table
			EntitySchema? schema = null;
			foreach (var kvp in snapshot.Entities)
			{
				if (kvp.Value.ClassName == entityType.Name)
				{
					schema = kvp.Value;
					break;
				}
			}

			if (schema != null)
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
		// Collect all pending entities (Added/Modified) for validation
		var allPendingEntities = new List<object>();
		var allDeletedEntities = new List<object>();

		foreach (var set in sets.Values)
		{
			var getPendingMethod = set.GetType().GetMethod("GetPendingEntities",
				BindingFlags.NonPublic | BindingFlags.Instance);
			var getDeletedMethod = set.GetType().GetMethod("GetDeletedEntities",
				BindingFlags.NonPublic | BindingFlags.Instance);

			if (getPendingMethod != null)
			{
				var pending = getPendingMethod.Invoke(set, null) as IEnumerable<object>;
				if (pending != null)
				{
					allPendingEntities.AddRange(pending);
				}
			}

			if (getDeletedMethod != null)
			{
				var deleted = getDeletedMethod.Invoke(set, null) as IEnumerable<object>;
				if (deleted != null)
				{
					allDeletedEntities.AddRange(deleted);
				}
			}
		}

		// Validate all pending entities (Add/Update) BEFORE making API calls
		if (_validator != null && allPendingEntities.Count > 0)
		{
			var result = new ValidationResult();

			foreach (var entity in allPendingEntities)
			{
				var entityType = entity.GetType();
				var tableName = EntityMapper.GetTableName(entityType);

				if (_currentSnapshot?.Entities.TryGetValue(tableName, out var schema) == true)
				{
					var context = new ValidationContext
					{
						TrackedEntities = allPendingEntities,
						Schema = schema,
						EntityType = entityType
					};

					var entityResult = _validator.Validate(entity, context);
					result.Merge(entityResult);
				}
			}

			if (!result.IsValid)
			{
				throw new ValidationException(result);
			}
		}

		// Validate FK constraints on DELETE operations
		if (allDeletedEntities.Count > 0)
		{
			await ValidateForeignKeyConstraintsOnDelete(allDeletedEntities);
		}

		// Now save changes
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

	private async Task ValidateForeignKeyConstraintsOnDelete(List<object> deletedEntities)
	{
		if (_currentSnapshot?.Entities == null || provider == null) return;

		foreach (var deletedEntity in deletedEntities)
		{
			var entityType = deletedEntity.GetType();

			// Match by ClassName (not TableName)
			var entitySchema = _currentSnapshot.Entities.Values
				.FirstOrDefault(e => e.ClassName == entityType.Name);

			if (entitySchema == null) continue;

			// Get PK value of entity being deleted
			var pkColumn = entitySchema.Columns.FirstOrDefault(c => c.IsPrimaryKey);
			if (pkColumn == null) continue;

			var pkProp = entityType.GetProperty(pkColumn.PropertyName);
			if (pkProp == null) continue;

			var pkValue = pkProp.GetValue(deletedEntity);
			if (pkValue == null) continue;

			// Check all other entities for FK references to this entity
			foreach (var otherEntity in _currentSnapshot.Entities.Values)
			{
				// Skip self
				if (otherEntity.TableName == entitySchema.TableName) continue;

				// Find FK columns pointing to this table
				var fkColumns = otherEntity.Columns
					.Where(c => c.IsForeignKey && c.ForeignKeyTable == entitySchema.TableName)
					.ToList();

				if (fkColumns.Count == 0) continue;

				// Check if any records reference this entity
				foreach (var fkColumn in fkColumns)
				{
					var onDeleteAction = fkColumn.OnDelete;

					// Check if sheet exists and has referencing data
					if (!await provider.SheetExistsAsync(otherEntity.TableName))
						continue;

					var dataRows = await provider.GetAllRowsAsync(otherEntity.TableName);
					if (dataRows.Count <= 1) continue; // Only header

					// Find FK column index
					var headerRow = dataRows[0];
					int fkColumnIndex = -1;
					for (int i = 0; i < headerRow.Count; i++)
					{
						if (headerRow[i]?.ToString() == fkColumn.PropertyName)
						{
							fkColumnIndex = i;
							break;
						}
					}

					if (fkColumnIndex < 0) continue;

					// Check for referencing rows
					var referencingRows = new List<int>();
					for (int i = 1; i < dataRows.Count; i++)
					{
						if (fkColumnIndex < dataRows[i].Count)
						{
							var fkValue = dataRows[i][fkColumnIndex]?.ToString();
							if (fkValue == pkValue.ToString())
							{
								referencingRows.Add(i);
							}
						}
					}

					if (referencingRows.Count > 0)
					{
						// Handle based on OnDelete action
						switch (onDeleteAction)
						{
							case ForeignKeyAction.Restrict:
							case ForeignKeyAction.NoAction:
								throw new InvalidOperationException(
									$"Cannot delete '{entitySchema.TableName}' with ID '{pkValue}' because " +
									$"{referencingRows.Count} record(s) in '{otherEntity.TableName}' reference it. " +
									$"Delete the dependent records first or change the relationship to use Cascade delete.");

							case ForeignKeyAction.Cascade:
								// Delete referencing rows (in reverse order to maintain indices)
								foreach (var rowIndex in referencingRows.OrderByDescending(x => x))
								{
									await provider.DeleteRowAsync(otherEntity.TableName, rowIndex);
								}
								break;

							case ForeignKeyAction.SetNull:
								// Set FK to null (only if column is nullable)
								if (fkColumn.IsNullable)
								{
									foreach (var rowIndex in referencingRows)
									{
										var cellAddress = GetCellAddress(fkColumnIndex, rowIndex);
										await provider.UpdateValueAsync(otherEntity.TableName, cellAddress, null);
									}
								}
								else
								{
									throw new InvalidOperationException(
										$"Cannot set FK '{fkColumn.PropertyName}' to NULL because it's not nullable.");
								}
								break;

							case ForeignKeyAction.SetDefault:
								// Set FK to default value
								if (fkColumn.DefaultValue != null)
								{
									foreach (var rowIndex in referencingRows)
									{
										var cellAddress = GetCellAddress(fkColumnIndex, rowIndex);
										await provider.UpdateValueAsync(otherEntity.TableName, cellAddress, fkColumn.DefaultValue);
									}
								}
								break;
						}
					}
				}
			}
		}
	}

	private string GetCellAddress(int columnIndex, int rowIndex)
	{
		// Convert column index to A1 notation (0 -> A, 1 -> B, etc.)
		string column = "";
		int col = columnIndex;
		while (col >= 0)
		{
			column = (char)('A' + (col % 26)) + column;
			col = col / 26 - 1;
		}
		return $"{column}{rowIndex + 1}";
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
