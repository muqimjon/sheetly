using Sheetly.Core.Abstractions;
using Sheetly.Core.Configuration;
using Sheetly.Core.Infrastructure;
using Sheetly.Core.Migration;
using Sheetly.Core.Migrations.Operations;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sheetly.Core.Migrations.Design;

/// <summary>
/// Entry point for design-time operations invoked by the CLI tool.
/// All operations execute within the project's own AssemblyLoadContext,
/// so no Sheetly types cross the context boundary — only JSON strings.
/// This mirrors EF Core's OperationExecutor pattern.
/// </summary>
public static class DesignTimeOperations
{
	/// <summary>
	/// Creates a new migration. Writes files to disk.
	/// Returns JSON: { "success":true, "migrationFile":"...", "snapshotFile":"...", "operations":["CreateTable",...] }
	/// Or: { "success":false, "error":"..." }
	/// </summary>
	public static string AddMigration(Type contextType, string name, string? outputDir)
	{
		try
		{
			var context = Activator.CreateInstance(contextType)!;

			outputDir ??= "Migrations";
			var modelBuilder = new ModelBuilder();
			InvokeOnModelCreating(contextType, context, modelBuilder);

			var currentSnapshot = SnapshotBuilder.BuildFromContext(contextType, modelBuilder.GetMetadata());

			string contextProjectDir = FindProjectRootFromDll(contextType.Assembly.Location);
			string finalPath = Path.Combine(contextProjectDir, outputDir);
			Directory.CreateDirectory(finalPath);

			MigrationSnapshot? previousSnapshot = LoadExistingSnapshot(contextType);

			var modelDiffer = new ModelDiffer();
			var operations = modelDiffer.GetDifferences(previousSnapshot, currentSnapshot);

			if (operations.Count == 0)
				return Error("No changes detected in the model.");

			var existingMigration = Directory.GetFiles(finalPath, "*.cs")
				.Where(f => !f.Contains("ModelSnapshot"))
				.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f)
					.EndsWith($"_{name}", StringComparison.OrdinalIgnoreCase));

			if (existingMigration != null)
				return Error($"A migration named '{name}' already exists: '{Path.GetFileName(existingMigration)}'");

			string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
			string migrationId = $"{timestamp}_{name}";
			string targetNamespace = $"{contextType.Namespace}.Migrations";

			var generator = new CSharpMigrationGenerator();
			string migrationCode = generator.GenerateMigration(name, migrationId, targetNamespace, operations);
			string csharpFileName = $"{migrationId}.cs";
			File.WriteAllText(Path.Combine(finalPath, csharpFileName), migrationCode);

			string contextName = contextType.Name.Replace("Context", "");
			var snapshotGenerator = new ModelSnapshotGenerator();
			string snapshotCode = snapshotGenerator.GenerateModelSnapshot(
				currentSnapshot, targetNamespace, contextName);
			string snapshotFileName = $"{contextName}ModelSnapshot.cs";
			File.WriteAllText(Path.Combine(finalPath, snapshotFileName), snapshotCode);

			return JsonSerializer.Serialize(new
			{
				success = true,
				migrationFile = csharpFileName,
				snapshotFile = snapshotFileName,
				operations = operations.Select(o => o.OperationType).ToArray()
			});
		}
		catch (Exception ex)
		{
			return Error(ex.InnerException?.Message ?? ex.Message);
		}
	}

	/// <summary>
	/// Removes the last migration. Reverts snapshot and deletes migration file.
	/// Returns JSON: { "success":true, "removedFile":"...", "snapshotFile":"..." }
	/// </summary>
	public static string RemoveMigration(Type contextType)
	{
		try
		{
			string contextProjectDir = FindProjectRootFromDll(contextType.Assembly.Location);
			string migrationsDir = Path.Combine(contextProjectDir, "Migrations");

			if (!Directory.Exists(migrationsDir))
				return Error("Migrations directory not found.");

			var migrationTypes = contextType.Assembly.GetExportedTypes()
				.Select(t => new { Type = t, Attr = t.GetCustomAttribute<MigrationAttribute>() })
				.Where(x => x.Attr != null)
				.OrderByDescending(x => x.Attr!.Id)
				.ToList();

			if (migrationTypes.Count == 0)
				return Error("No migrations to remove.");

			var lastMigrationType = migrationTypes[0].Type;
			string migrationId = migrationTypes[0].Attr!.Id;

			var migrationFile = Directory.GetFiles(migrationsDir, "*.cs")
				.FirstOrDefault(f => !f.Contains("ModelSnapshot") &&
									 Path.GetFileNameWithoutExtension(f) == migrationId);

			if (migrationFile == null)
				return Error($"Migration file for '{migrationId}' not found.");

			string contextName = contextType.Name.Replace("Context", "");
			string snapshotClassName = $"{contextName}ModelSnapshot";
			string targetNamespace = $"{contextType.Namespace}.Migrations";

			var snapshotType = contextType.Assembly.GetExportedTypes()
				.FirstOrDefault(t => t.Name == snapshotClassName && t.Namespace == targetNamespace)
				?? throw new Exception($"ModelSnapshot class '{snapshotClassName}' not found.");

			var currentSnapshot = (MigrationSnapshot?)Activator.CreateInstance(snapshotType)
				?? throw new Exception("Failed to instantiate ModelSnapshot.");

			var lastMigration = (Migration)Activator.CreateInstance(lastMigrationType)!;
			var downBuilder = new MigrationBuilder();
			lastMigration.Down(downBuilder);

			var revertedSnapshot = RevertSnapshot(currentSnapshot, downBuilder.GetOperations());

			var snapshotGenerator = new ModelSnapshotGenerator();
			string snapshotCode = snapshotGenerator.GenerateModelSnapshot(revertedSnapshot, targetNamespace, contextName);
			string snapshotFilePath = Path.Combine(migrationsDir, $"{snapshotClassName}.cs");

			File.Delete(migrationFile);
			File.WriteAllText(snapshotFilePath, snapshotCode);

			return JsonSerializer.Serialize(new
			{
				success = true,
				removedFile = Path.GetFileName(migrationFile),
				snapshotFile = $"{snapshotClassName}.cs"
			});
		}
		catch (Exception ex)
		{
			return Error(ex.InnerException?.Message ?? ex.Message);
		}
	}

	/// <summary>
	/// Gets a text schema from the latest snapshot.
	/// Returns JSON: { "success":true, "script":"..." }
	/// </summary>
	public static string GetSchemaScript(Type contextType)
	{
		try
		{
			var snapshotType = contextType.Assembly.GetTypes()
				.FirstOrDefault(t => t.Name.EndsWith("ModelSnapshot") && t.IsSubclassOf(typeof(MigrationSnapshot)));

			if (snapshotType == null)
				return Error("Snapshot not found. Run 'migrations add' first.");

			var snapshot = (MigrationSnapshot)Activator.CreateInstance(snapshotType)!;
			var sb = new StringBuilder();
			sb.AppendLine($"--- Sheetly Schema Script (Generated at {DateTime.Now}) ---");

			foreach (var entity in snapshot.Entities.Values)
			{
				sb.AppendLine($"Sheet: {entity.TableName}");
				foreach (var col in entity.Columns)
				{
					string pk = col.IsPrimaryKey ? " [PK]" : "";
					string fk = col.IsForeignKey ? $" [FK → {col.ForeignKeyTable}]" : "";
					string req = col.IsRequired ? " [Required]" : "";
					sb.AppendLine($"  - {col.PropertyName} ({col.DataType}){pk}{fk}{req}");
				}
				sb.AppendLine();
			}

			return JsonSerializer.Serialize(new { success = true, script = sb.ToString() });
		}
		catch (Exception ex)
		{
			return Error(ex.InnerException?.Message ?? ex.Message);
		}
	}

	/// <summary>
	/// Applies all pending migrations using the context's configured provider.
	/// Returns JSON: { "success":true, "applied":["20240101_Init",...], "total":2 }
	/// </summary>
	public static async Task<string> UpdateDatabaseAsync(Type contextType, string? connectionString = null)
	{
		try
		{
			var (provider, migrationService) = CreateProviderFromContext(contextType, connectionString);
			await provider.InitializeAsync();

			var facade = new DatabaseFacade(provider, migrationService, contextType);
			var pending = await facade.GetPendingMigrationsAsync();

			if (pending.Count == 0)
				return JsonSerializer.Serialize(new { success = true, applied = Array.Empty<string>(), total = 0, message = "Database is up to date." });

			await facade.MigrateAsync();

			return JsonSerializer.Serialize(new { success = true, applied = pending, total = pending.Count });
		}
		catch (Exception ex)
		{
			return Error(ex.InnerException?.Message ?? ex.Message);
		}
	}

	/// <summary>
	/// Drops the database (clears all sheets).
	/// Returns JSON: { "success":true }
	/// </summary>
	public static async Task<string> DropDatabaseAsync(Type contextType, string? connectionString = null)
	{
		try
		{
			var (provider, migrationService) = CreateProviderFromContext(contextType, connectionString);
			await provider.InitializeAsync();

			var facade = new DatabaseFacade(provider, migrationService, contextType);
			await facade.DropDatabaseAsync();

			return JsonSerializer.Serialize(new { success = true });
		}
		catch (Exception ex)
		{
			return Error(ex.InnerException?.Message ?? ex.Message);
		}
	}

	/// <summary>
	/// Scaffolds model classes from the remote provider's migration history.
	/// Returns JSON: { "success":true, "files":["Product.cs","Category.cs"] }
	/// </summary>
	public static async Task<string> ScaffoldAsync(Type contextType, string? outputDir, string? connectionString = null)
	{
		try
		{
			var (provider, _) = CreateProviderFromContext(contextType, connectionString);
			await provider.InitializeAsync();

			var rows = await provider.GetAllRowsAsync("__SheetlyHistory__");
			if (rows.Count <= 1)
				return Error("Migration history not found.");

			var snapshotJson = rows.Last()[2].ToString()!;
			var snapshot = JsonSerializer.Deserialize<MigrationSnapshot>(snapshotJson)!;

			string contextProjectDir = FindProjectRootFromDll(contextType.Assembly.Location);
			string finalPath = Path.Combine(contextProjectDir, outputDir ?? "Models/Scaffolded");
			Directory.CreateDirectory(finalPath);

			var files = new List<string>();
			foreach (var entity in snapshot.Entities.Values)
			{
				var code = GenerateClassCode(entity);
				var fileName = $"{entity.ClassName}.cs";
				File.WriteAllText(Path.Combine(finalPath, fileName), code);
				files.Add(fileName);
			}

			return JsonSerializer.Serialize(new { success = true, files });
		}
		catch (Exception ex)
		{
			return Error(ex.InnerException?.Message ?? ex.Message);
		}
	}

	#region Private helpers

	private static void InvokeOnModelCreating(Type contextType, object context, ModelBuilder modelBuilder)
	{
		var method = contextType.GetMethod("OnModelCreating",
			BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		method?.Invoke(context, [modelBuilder]);
	}

	private static MigrationSnapshot? LoadExistingSnapshot(Type contextType)
	{
		string contextName = contextType.Name.Replace("Context", "");
		string snapshotClassName = $"{contextName}ModelSnapshot";
		var snapshotType = contextType.Assembly.GetExportedTypes()
			.FirstOrDefault(t => t.Name == snapshotClassName &&
							t.Namespace == $"{contextType.Namespace}.Migrations");
		if (snapshotType == null) return null;
		return Activator.CreateInstance(snapshotType) as MigrationSnapshot;
	}

	/// <summary>
	/// Creates the ISheetsProvider by calling the context's OnConfiguring,
	/// just like EF Core creates the DbConnection from the DbContext configuration.
	/// Works with any provider (Google Sheets, Excel, or future implementations).
	/// </summary>
	private static (ISheetsProvider provider, IMigrationService? migrationService) CreateProviderFromContext(
		Type contextType, string? connectionString)
	{
		var context = Activator.CreateInstance(contextType)!;
		var options = new SheetsOptions();

		if (!string.IsNullOrEmpty(connectionString))
			options.ConnectionString = connectionString;

		var method = contextType.GetMethod("OnConfiguring",
			BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		method?.Invoke(context, [options]);

		var provider = options.Provider
			?? throw new InvalidOperationException(
				"ISheetsProvider not configured. Call UseGoogleSheets or UseExcel in OnConfiguring.");

		return (provider, options.MigrationService);
	}

	private static string FindProjectRootFromDll(string dllPath)
	{
		var dir = new DirectoryInfo(Path.GetDirectoryName(dllPath)!);
		while (dir != null && !dir.GetFiles("*.csproj").Any())
			dir = dir.Parent;
		return dir?.FullName ?? Path.GetDirectoryName(dllPath)!;
	}

	private static string Error(string message)
		=> JsonSerializer.Serialize(new { success = false, error = message });

	private static MigrationSnapshot RevertSnapshot(MigrationSnapshot current, List<MigrationOperation> downOps)
	{
		var entities = current.Entities
			.ToDictionary(kvp => kvp.Key, kvp => CloneEntity(kvp.Value));

		foreach (var op in downOps)
		{
			switch (op)
			{
				case DropColumnOperation drop:
					if (entities.TryGetValue(drop.Table, out var eDrop))
						eDrop.Columns.RemoveAll(c => c.Name == drop.Name);
					break;

				case AddColumnOperation add:
					if (entities.TryGetValue(add.Table, out var eAdd))
						eAdd.Columns.Add(new ColumnSchema
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
					if (entities.TryGetValue(alter.Table, out var eAlter))
					{
						var col = eAlter.Columns.FirstOrDefault(c => c.Name == alter.Name);
						if (col != null)
						{
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

	private static string GenerateClassCode(EntitySchema entity)
	{
		var sb = new StringBuilder();
		sb.AppendLine("using System.ComponentModel.DataAnnotations;");
		sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
		sb.AppendLine();
		sb.AppendLine($"namespace {entity.Namespace}.Scaffolded;");
		sb.AppendLine();
		sb.AppendLine($"[Table(\"{entity.TableName}\")]");
		sb.AppendLine($"public class {entity.ClassName}");
		sb.AppendLine("{");
		foreach (var col in entity.Columns)
		{
			if (col.IsPrimaryKey) sb.AppendLine("    [Key]");
			if (col.IsForeignKey) sb.AppendLine($"    [ForeignKey(\"{col.ForeignKeyTable}\")]");
			var type = col.DataType;
			if (col.IsNullable && type != "String" && !type.EndsWith("?")) type += "?";
			sb.AppendLine($"    public {type} {col.PropertyName} {{ get; set; }}");
			sb.AppendLine();
		}
		sb.AppendLine("}");
		return sb.ToString();
	}

	#endregion
}
