using Sheetly.CLI.Helpers;
using Sheetly.Core.Migration;
using Sheetly.Core.Migrations;
using Sheetly.Core.Migrations.Design;
using Sheetly.Core.Migrations.Operations;
using System.CommandLine;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sheetly.CLI.Commands;

public class RemoveCommand : Command
{
	private readonly Option<string?> _projectOption = new("--project", ["-p"]);

	public RemoveCommand() : base("remove", "Remove the last migration")
	{
		this.Add(_projectOption);
		this.SetAction(async (parseResult, ct) => await ExecuteAsync(parseResult.GetValue(_projectOption), ct));
	}

	private async Task ExecuteAsync(string? projectPath, CancellationToken ct)
	{
		string dllPath = CliHelper.FindProjectDll(false, projectPath);
		if (string.IsNullOrEmpty(dllPath)) return;

		try
		{
			var assembly = Assembly.LoadFrom(Path.GetFullPath(dllPath));
			var contextType = assembly.GetExportedTypes().FirstOrDefault(t => CliHelper.IsSubclassOfSheetsContext(t))
				?? throw new Exception("SheetsContext not found.");

			string contextProjectDir = CliHelper.FindProjectRootFromDll(contextType.Assembly.Location);
			string migrationsDir = Path.Combine(contextProjectDir, "Migrations");

			if (!Directory.Exists(migrationsDir))
			{
				Console.WriteLine("⚠️ Migrations directory not found.");
				return;
			}

			// Find last migration type by MigrationAttribute ID (sorted descending)
			var migrationTypes = assembly.GetExportedTypes()
				.Where(t => t.GetCustomAttribute<MigrationAttribute>() != null)
				.OrderByDescending(t => t.GetCustomAttribute<MigrationAttribute>()!.Id)
				.ToList();

			if (migrationTypes.Count == 0)
			{
				Console.WriteLine("⚠️ No migrations to remove.");
				return;
			}

			var lastMigrationType = migrationTypes[0];
			string migrationId = lastMigrationType.GetCustomAttribute<MigrationAttribute>()!.Id;

			// Find the corresponding .cs file on disk
			var migrationFile = Directory.GetFiles(migrationsDir, "*.cs")
				.FirstOrDefault(f => !f.Contains("ModelSnapshot") &&
									 Path.GetFileNameWithoutExtension(f) == migrationId);

			if (migrationFile == null)
			{
				Console.WriteLine($"⚠️ Migration file for '{migrationId}' not found. It may have already been removed from disk.");
				return;
			}

			// Load the current snapshot from the compiled assembly
			string contextName = contextType.Name.Replace("Context", "");
			string snapshotClassName = $"{contextName}ModelSnapshot";
			string targetNamespace = $"{contextType.Namespace}.Migrations";

			var snapshotType = assembly.GetExportedTypes()
				.FirstOrDefault(t => t.Name == snapshotClassName && t.Namespace == targetNamespace)
				?? throw new Exception($"ModelSnapshot class '{snapshotClassName}' not found.");

			var currentSnapshot = Activator.CreateInstance(snapshotType) as MigrationSnapshot
				?? throw new Exception("Failed to instantiate ModelSnapshot.");

			// Get the Down() operations to know what to revert
			var lastMigration = Activator.CreateInstance(lastMigrationType) as Migration
				?? throw new Exception("Failed to instantiate migration.");

			var downBuilder = new Sheetly.Core.Migrations.MigrationBuilder();
			lastMigration.Down(downBuilder);

			// Apply Down operations to produce the previous snapshot state
			var revertedSnapshot = RevertSnapshot(currentSnapshot, downBuilder.GetOperations());

			// Regenerate ModelSnapshot C# file
			var generator = new ModelSnapshotGenerator();
			string snapshotCode = generator.GenerateModelSnapshot(revertedSnapshot, targetNamespace, contextName);
			string snapshotFilePath = Path.Combine(migrationsDir, $"{snapshotClassName}.cs");

			File.Delete(migrationFile);
			await File.WriteAllTextAsync(snapshotFilePath, snapshotCode, ct);

			Console.WriteLine($"✅ Migration removed: '{Path.GetFileName(migrationFile)}'");
			Console.WriteLine($"✅ Model snapshot reverted: '{snapshotClassName}.cs'");
		}
		catch (Exception ex) { Console.WriteLine($"❌ Error: {ex.Message}"); }
	}

	/// <summary>
	/// Applies Down operations in reverse to produce the snapshot state before the migration was added.
	/// </summary>
	private static MigrationSnapshot RevertSnapshot(MigrationSnapshot current, List<MigrationOperation> downOps)
	{
		var entities = current.Entities.ToDictionary(kvp => kvp.Key, kvp => CloneEntity(kvp.Value));

		foreach (var op in downOps)
		{
			switch (op)
			{
				case DropColumnOperation drop:
					if (entities.TryGetValue(drop.Table, out var entity))
						entity.Columns.RemoveAll(c => c.Name == drop.Name);
					break;

				case AddColumnOperation add:
					if (entities.TryGetValue(add.Table, out var entityToAddCol))
						entityToAddCol.Columns.Add(new ColumnSchema
						{
							Name = add.Name,
							PropertyName = add.Name,
							DataType = add.ClrType.Name,
							IsNullable = add.IsNullable,
							IsRequired = add.IsRequired,
							IsPrimaryKey = add.IsPrimaryKey,
							IsAutoIncrement = add.IsPrimaryKey,
							IsForeignKey = add.IsForeignKey,
							ForeignKeyTable = add.ForeignKeyTable,
							ForeignKeyColumn = add.ForeignKeyColumn,
							IsUnique = add.IsUnique,
							MaxLength = add.MaxLength,
							MinLength = add.MinLength,
							DefaultValue = add.DefaultValue,
							CheckConstraint = add.CheckConstraint,
							IsComputed = add.IsComputed,
							ComputedColumnSql = add.ComputedColumnSql,
							IsConcurrencyToken = add.IsConcurrencyToken,
							Comment = add.Comment
						});
					break;

				case DropTableOperation dropTable:
					entities.Remove(dropTable.Name);
					break;

				case CreateTableOperation createTable:
					entities[createTable.Name] = new EntitySchema
					{
						TableName = createTable.Name,
						ClassName = createTable.ClassName ?? createTable.Name,
						Columns = createTable.Columns.Select(c => new ColumnSchema
						{
							Name = c.Name,
							PropertyName = c.Name,
							DataType = c.ClrType.Name,
							IsNullable = c.IsNullable,
							IsRequired = c.IsRequired,
							IsPrimaryKey = c.IsPrimaryKey,
							IsAutoIncrement = c.IsPrimaryKey,
							IsForeignKey = c.IsForeignKey,
							ForeignKeyTable = c.ForeignKeyTable,
							ForeignKeyColumn = c.ForeignKeyColumn
						}).ToList(),
						Relationships = []
					};
					break;

				case AlterColumnOperation alter:
					if (entities.TryGetValue(alter.Table, out var entityToAlter))
					{
						var col = entityToAlter.Columns.FirstOrDefault(c => c.Name == alter.Name);
						if (col != null)
						{
							// Down's AlterColumn values are the values to restore
							if (alter.ClrType != null) col.DataType = alter.ClrType.Name;
							if (alter.IsNullable.HasValue) col.IsNullable = alter.IsNullable.Value;
							if (alter.MaxLength.HasValue) col.MaxLength = alter.MaxLength;
							if (alter.DefaultValue != null) col.DefaultValue = alter.DefaultValue;
						}
					}
					break;
			}
		}

		return new MigrationSnapshot
		{
			Entities = entities,
			Version = current.Version,
			LastUpdated = DateTime.UtcNow,
			ModelHash = CalculateHash(entities)
		};
	}

	private static EntitySchema CloneEntity(EntitySchema src) => new()
	{
		TableName = src.TableName,
		ClassName = src.ClassName,
		Namespace = src.Namespace,
		Columns = src.Columns.Select(c => new ColumnSchema
		{
			Name = c.Name,
			PropertyName = c.PropertyName,
			DataType = c.DataType,
			IsNullable = c.IsNullable,
			IsRequired = c.IsRequired,
			IsPrimaryKey = c.IsPrimaryKey,
			IsAutoIncrement = c.IsAutoIncrement,
			IsForeignKey = c.IsForeignKey,
			ForeignKeyTable = c.ForeignKeyTable,
			ForeignKeyColumn = c.ForeignKeyColumn,
			IsUnique = c.IsUnique,
			IndexName = c.IndexName,
			MaxLength = c.MaxLength,
			MinLength = c.MinLength,
			DefaultValue = c.DefaultValue,
			DefaultValueSql = c.DefaultValueSql,
			MinValue = c.MinValue,
			MaxValue = c.MaxValue,
			Precision = c.Precision,
			Scale = c.Scale,
			CheckConstraint = c.CheckConstraint,
			IsComputed = c.IsComputed,
			ComputedColumnSql = c.ComputedColumnSql,
			IsStored = c.IsStored,
			IsConcurrencyToken = c.IsConcurrencyToken,
			Comment = c.Comment
		}).ToList(),
		Relationships = src.Relationships.ToList()
	};

	private static string CalculateHash(Dictionary<string, EntitySchema> entities)
	{
		var structural = entities
			.OrderBy(e => e.Key)
			.ToDictionary(
				e => e.Key,
				e => new
				{
					e.Value.TableName,
					Columns = e.Value.Columns.Select(c => new
					{
						c.Name,
						c.DataType,
						c.IsPrimaryKey,
						c.IsAutoIncrement,
						c.IsForeignKey,
						c.ForeignKeyTable,
						c.ForeignKeyColumn
					}).ToList()
				});

		var json = JsonSerializer.Serialize(structural, new JsonSerializerOptions { WriteIndented = false });
		return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
	}
}