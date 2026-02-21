using Sheetly.CLI.Helpers;
using Sheetly.Core;
using Sheetly.Core.Migrations;
using Sheetly.Core.Migrations.Operations;
using Sheetly.Google;
using System.CommandLine;
using System.Reflection;

namespace Sheetly.CLI.Commands;

public class UpdateCommand : Command
{
private readonly Option<string?> _projectOption = new("--project", ["-p"]) { Description = "Manual path to DLL" };
private readonly Option<bool> _noBuildOption = new("--no-build", ["-n"]) { Description = "Do not build project before action" };

public UpdateCommand() : base("update", "Apply migrations to the database")
{
this.Add(_projectOption);
this.Add(_noBuildOption);

this.SetAction(async (parseResult, ct) =>
{
bool noBuild = parseResult.GetValue(_noBuildOption);
string? projectPath = parseResult.GetValue(_projectOption);
await ExecuteAsync(noBuild, projectPath, ct);
});
}

private async Task ExecuteAsync(bool noBuild, string? projectPath, CancellationToken ct)
{
string dllPath = CliHelper.FindProjectDll(noBuild, projectPath);
if (string.IsNullOrEmpty(dllPath)) return;

try
{
var assembly = Assembly.LoadFrom(Path.GetFullPath(dllPath));
var contextType = assembly.GetExportedTypes().FirstOrDefault(t => CliHelper.IsSubclassOfSheetsContext(t))
?? throw new Exception("SheetsContext not found.");

string contextProjectDir = CliHelper.FindProjectRootFromDll(contextType.Assembly.Location);
string? connStr = CliHelper.GetConnectionString(contextProjectDir) ?? throw new Exception("ConnectionString not found.");

// Initialize Google Sheets Provider
var factoryType = assembly.GetExportedTypes().FirstOrDefault(t => t.Name == "GoogleSheetsFactory")
  ?? typeof(GoogleSheetsFactory);

var method = factoryType.GetMethods(BindingFlags.Public | BindingFlags.Static)
.FirstOrDefault(m => m.Name == "CreateContextAsync" && m.IsGenericMethod && m.GetParameters().Length == 1)
?.MakeGenericMethod(contextType);

if (method == null) throw new Exception("CreateContextAsync not found.");

Console.WriteLine("⏳ Connecting to Google Sheets...");
var task = (Task)method.Invoke(null, [connStr])!;
await task;

var context = (SheetsContext)((dynamic)task).Result;
if (context.provider == null) throw new Exception("Provider is required.");

// Initialize Migration Service
var migrationService = new GoogleMigrationService(context.provider);

// 1. Get applied migrations
var appliedMigrations = await migrationService.GetAppliedMigrationsAsync();

// 2. Find local migrations
var migrationTypes = assembly.GetTypes()
.Where(t => t.IsSubclassOf(typeof(Migration)) && !t.IsAbstract)
.Select(t => new
{
Type = t,
Attribute = t.GetCustomAttribute<MigrationAttribute>()
})
.Where(x => x.Attribute != null)
.OrderBy(x => x.Attribute!.Id)
.ToList();

if (migrationTypes.Count == 0)
{
Console.WriteLine("⚠️ No migrations found in the project.");
return;
}

// 3. Filter pending migrations
var pendingMigrations = migrationTypes
.Where(x => !appliedMigrations.Contains(x.Attribute!.Id))
.ToList();

if (pendingMigrations.Count == 0)
{
Console.WriteLine("✅ Database is up to date.");
return;
}

Console.WriteLine($"🚀 Found {pendingMigrations.Count} pending migration(s).");

// 4. Load current snapshot to enrich operations with ClassName
var snapshotType = assembly.GetTypes()
.FirstOrDefault(t => t.Name.EndsWith("ModelSnapshot") && t.IsSubclassOf(typeof(Sheetly.Core.Migration.MigrationSnapshot)));

Sheetly.Core.Migration.MigrationSnapshot? currentSnapshot = null;
if (snapshotType != null)
{
// Instantiate snapshot (constructor populates Entities)
currentSnapshot = (Sheetly.Core.Migration.MigrationSnapshot?)Activator.CreateInstance(snapshotType);
}


// 5. Apply migrations
foreach (var pm in pendingMigrations)
{
var migrationId = pm.Attribute!.Id;
Console.Write($"Applying {migrationId}... ");

var migration = (Migration)Activator.CreateInstance(pm.Type)!;
var builder = new Core.Migrations.MigrationBuilder();
migration.Up(builder);

var operations = builder.GetOperations();

// Enrich operations with metadata from snapshot (ClassName, IsAutoIncrement)
if (currentSnapshot != null)
{
foreach (var op in operations.OfType<CreateTableOperation>())
{
if (currentSnapshot.Entities.TryGetValue(op.Name, out var entity))
{
op.ClassName = entity.ClassName;

// Enrich columns with IsAutoIncrement from snapshot
foreach (var col in op.Columns)
{
var snapshotCol = entity.Columns.FirstOrDefault(c => c.Name == col.Name);
if (snapshotCol != null)
{
col.IsAutoIncrement = snapshotCol.IsAutoIncrement;
// Primary keys are automatically unique (EF Core behavior)
if (snapshotCol.IsPrimaryKey)
{
col.IsUnique = true;
}
if (snapshotCol.IsAutoIncrement)
{
col.IsAutoIncrement = true;
}
}
}
}
}
}

	// Enrich AddColumn operations
	foreach (var op in operations.OfType<AddColumnOperation>())
	{
		if (currentSnapshot.Entities.TryGetValue(op.Table, out var entity))
		{
			var snapshotCol = entity.Columns.FirstOrDefault(c => c.Name == op.Name);
			if (snapshotCol != null)
			{
				op.IsAutoIncrement = snapshotCol.IsAutoIncrement;
				// Primary keys are automatically unique
				if (snapshotCol.IsPrimaryKey)
				{
					op.IsUnique = true;
				}
				// Store ClassName for schema table
				op.ClassName = entity.ClassName;
			}
		}
	}

await migrationService.ApplyMigrationAsync(operations, migrationId);

Console.WriteLine("Done.");
}

Console.WriteLine("✅ All migrations applied successfully.");
}
catch (Exception ex)
{
Console.WriteLine($"❌ Error: {ex.Message}");
if (ex.InnerException != null) Console.WriteLine($"🔍 Detail: {ex.InnerException.Message}");
}
}
}
