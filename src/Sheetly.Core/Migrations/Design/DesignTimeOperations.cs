using Sheetly.Core.Abstractions;
using Sheetly.Core.Configuration;
using Sheetly.Core.Infrastructure;
using Sheetly.Core.Internal;
using Sheetly.Core.Migration;
using Sheetly.Core.Migrations.Operations;
using System.Reflection;
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
	/// Resolves the migrations namespace for a context type.
	/// Falls back to the assembly name when the context is in the global namespace (null namespace).
	/// </summary>
	public static string ResolveNamespace(Type contextType)
	{
		if (!string.IsNullOrEmpty(contextType.Namespace))
			return $"{contextType.Namespace}.Migrations";

		var asmName = contextType.Assembly.GetName().Name;
		if (!string.IsNullOrEmpty(asmName))
			return $"{asmName}.Migrations";

		return "App.Migrations";
	}

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

			if (existingMigration is not null)
				return Error($"A migration named '{name}' already exists: '{Path.GetFileName(existingMigration)}'");

			string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
			string migrationId = $"{timestamp}_{name}";
			string targetNamespace = ResolveNamespace(contextType);

			var downOperations = modelDiffer.GetDifferences(currentSnapshot, previousSnapshot ?? new MigrationSnapshot());

			var generator = new CSharpMigrationGenerator();
			string migrationCode = generator.GenerateMigration(name, migrationId, targetNamespace, operations, downOperations);
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
				.Where(x => x.Attr is not null)
				.OrderByDescending(x => x.Attr!.Id)
				.ToList();

			if (migrationTypes.Count == 0)
				return Error("No migrations to remove.");

			var lastMigrationType = migrationTypes[0].Type;
			string migrationId = migrationTypes[0].Attr!.Id;

			var migrationFile = Directory.GetFiles(migrationsDir, "*.cs")
				.FirstOrDefault(f => !f.Contains("ModelSnapshot") &&
									 Path.GetFileNameWithoutExtension(f) == migrationId);

			if (migrationFile is null)
				return Error($"Migration file for '{migrationId}' not found.");

			string contextName = contextType.Name.Replace("Context", "");
			string snapshotClassName = $"{contextName}ModelSnapshot";
			string targetNamespace = ResolveNamespace(contextType);

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

			if (snapshotType is null)
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
	/// Reverts the last applied migration against the database (runs Down() and removes history).
	/// Returns JSON: { "success":true, "rolledBack":"20240101_Init" } or rolledBack null when none.
	/// </summary>
	public static async Task<string> RollbackDatabaseAsync(Type contextType, string? connectionString = null)
	{
		try
		{
			var (provider, migrationService) = CreateProviderFromContext(contextType, connectionString);
			await provider.InitializeAsync();

			var facade = new DatabaseFacade(provider, migrationService, contextType);
			var rolledBack = await facade.RollbackLastAsync();

			return JsonSerializer.Serialize(new { success = true, rolledBack });
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
			var snapshot = LoadExistingSnapshot(contextType);
			if (snapshot is null || snapshot.Entities.Count == 0)
			{
				// Database-first: no local migrations — read the schema straight from __SheetlySchema__.
				var (provider, _) = CreateProviderFromContext(contextType, connectionString);
				await provider.InitializeAsync();
				snapshot = await SchemaReader.ReadAsync(provider);
			}

			if (snapshot.Entities.Count == 0)
				return Error("No model snapshot found and '__SheetlySchema__' is empty. Create migrations first, or point at a spreadsheet that already has a Sheetly schema.");

			string contextProjectDir = FindProjectRootFromDll(contextType.Assembly.Location);
			string finalPath = Path.Combine(contextProjectDir, outputDir ?? "Models/Scaffolded");
			Directory.CreateDirectory(finalPath);

			var files = new List<string>();
			var usedClassNames = new HashSet<string>(StringComparer.Ordinal);
			var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var entity in snapshot.Entities.Values)
			{
				var className = ResolveIdentifier(entity.ClassName, $"class name for table '{entity.TableName}'");
				if (className is null) continue;
				className = DedupeIdentifier(className, usedClassNames);

				var fileName = UniqueFileName(className.TrimStart('@'), usedFileNames);
				File.WriteAllText(Path.Combine(finalPath, fileName), GenerateClassCode(entity, className));
				files.Add(fileName);
			}

			await Task.CompletedTask;
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
		string targetNamespace = ResolveNamespace(contextType);
		var snapshotType = contextType.Assembly.GetExportedTypes()
			.FirstOrDefault(t => t.Name == snapshotClassName &&
							t.Namespace == targetNamespace);
		if (snapshotType is null) return null;
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
		while (dir is not null && !dir.GetFiles("*.csproj").Any())
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
						if (col is not null)
						{
							if (alter.ClrType is not null) col.DataType = alter.ClrType.Name;
							if (alter.IsNullable.HasValue) col.IsNullable = alter.IsNullable.Value;
							if (alter.MaxLength.HasValue) col.MaxLength = alter.MaxLength;
							if (alter.DefaultValue is not null) col.DefaultValue = alter.DefaultValue;
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
			ModelHash = ModelHasher.Calculate(entities)
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

	private static string? ResolveIdentifier(string identifier, string what)
	{
		if (IdentifierValidator.IsValid(identifier)) return identifier;

		var sanitized = IdentifierValidator.Sanitize(identifier);
		if (sanitized.Length == 0)
		{
			Console.WriteLine($"Warning: skipped invalid {what} '{identifier}'.");
			return null;
		}

		Console.WriteLine($"Warning: {what} '{identifier}' sanitized to '{sanitized}'.");
		return sanitized;
	}

	private static string DedupeIdentifier(string identifier, HashSet<string> used)
	{
		var bare = identifier.TrimStart('@');
		if (used.Add(bare)) return identifier;
		for (int i = 2; ; i++)
			if (used.Add($"{bare}{i}")) return $"{bare}{i}";
	}

	private static readonly HashSet<string> ReservedFileNames = new(StringComparer.OrdinalIgnoreCase)
	{
		"CON", "PRN", "AUX", "NUL",
		"COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
		"LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
	};

	internal static string UniqueFileName(string baseName, HashSet<string> used)
	{
		if (ReservedFileNames.Contains(baseName)) baseName = "_" + baseName;
		var name = baseName;
		for (int i = 2; !used.Add(name); i++)
			name = $"{baseName}_{i}";
		return Path.GetFileName($"{name}.cs");
	}

	private static string GetScaffoldType(string dataType, string tableName)
	{
		switch (dataType)
		{
			case "Int32": return "int";
			case "Int64": return "long";
			case "Int16": return "short";
			case "Byte": return "byte";
			case "String": return "string";
			case "Boolean": return "bool";
			case "Decimal": return "decimal";
			case "Double": return "double";
			case "Single": return "float";
			case "DateTime": return "DateTime";
			case "DateTimeOffset": return "DateTimeOffset";
			case "TimeSpan": return "TimeSpan";
			case "Guid": return "Guid";
			default:
				Console.WriteLine($"Warning: unknown data type '{dataType}' in '{tableName}' scaffolded as string.");
				return "string";
		}
	}

	internal static string GenerateClassCode(EntitySchema entity, string className)
	{
		var sb = new StringBuilder();
		sb.AppendLine("using System.ComponentModel.DataAnnotations;");
		sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
		sb.AppendLine();
		sb.AppendLine($"namespace {IdentifierValidator.SanitizeNamespace(entity.Namespace)}.Scaffolded;");
		sb.AppendLine();
		sb.AppendLine($"[Table(\"{CSharpHelper.EscapeStringLiteral(entity.TableName)}\")]");
		sb.AppendLine($"public class {className}");
		sb.AppendLine("{");
		var usedMembers = new HashSet<string>(StringComparer.Ordinal) { className.TrimStart('@') };
		foreach (var col in entity.Columns)
		{
			var propertyName = ResolveIdentifier(col.PropertyName, $"property name in '{entity.TableName}'");
			if (propertyName is null) continue;
			propertyName = DedupeIdentifier(propertyName, usedMembers);

			if (col.IsPrimaryKey) sb.AppendLine("    [Key]");
			if (col.IsForeignKey) sb.AppendLine($"    [ForeignKey(\"{CSharpHelper.EscapeStringLiteral(col.ForeignKeyTable ?? string.Empty)}\")]");
			var type = GetScaffoldType(col.DataType, entity.TableName);
			if (col.IsNullable) type += "?";
			sb.AppendLine($"    public {type} {propertyName} {{ get; set; }}");
			sb.AppendLine();
		}
		sb.AppendLine("}");
		return sb.ToString();
	}

	#endregion
}
