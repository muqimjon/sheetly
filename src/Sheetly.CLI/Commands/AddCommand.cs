using Sheetly.CLI.Helpers;
using Sheetly.Core.Migration;
using Sheetly.Core.Migrations;
using Sheetly.Core.Migrations.Design;
using System.CommandLine;
using System.Reflection;

namespace Sheetly.CLI.Commands;

public class AddCommand : Command
{
	private readonly Argument<string?> _nameArg = new("name") { Arity = ArgumentArity.ZeroOrOne, Description = "Migration name" };
	private readonly Option<string?> _projectOption = new("--project", ["-p"]);
	private readonly Option<bool> _noBuildOption = new("--no-build", ["-n"]);
	private readonly Option<string?> _outputDirOption = new("--output-dir", ["-o"]);

	public AddCommand() : base("add", "Add a new migration")
	{
		this.Add(_nameArg);
		this.Add(_projectOption);
		this.Add(_noBuildOption);
		this.Add(_outputDirOption);

		this.SetAction(async (parseResult, ct) =>
		{
			string? name = parseResult.GetValue(_nameArg);
			bool noBuild = parseResult.GetValue(_noBuildOption);
			string? projectPath = parseResult.GetValue(_projectOption);
			string? outputDir = parseResult.GetValue(_outputDirOption);
			await ExecuteAsync(name, noBuild, projectPath, outputDir, ct);
		});
	}

	private async Task ExecuteAsync(string? name, bool noBuild, string? projectPath, string? outputDir, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			Console.Write("Enter migration name: ");
			name = Console.ReadLine();
			if (string.IsNullOrWhiteSpace(name)) return;
		}

		string dllPath = CliHelper.FindProjectDll(noBuild, projectPath);
		if (string.IsNullOrEmpty(dllPath)) return;

		try
		{
			var (assembly, loadContext) = CliHelper.LoadAssemblyIsolated(dllPath);
			var contextType = assembly.GetExportedTypes().FirstOrDefault(t => CliHelper.IsSubclassOfSheetsContext(t))
				?? throw new Exception("SheetsContext not found.");

			var context = Activator.CreateInstance(contextType)!;
			string contextProjectDir = CliHelper.FindProjectRootFromDll(contextType.Assembly.Location);

			outputDir ??= "Migrations";

			// Use isolated Sheetly.Core to avoid MVID mismatch across load contexts.
			// CLI ships its own copy of Sheetly.Core; the project has a freshly built copy.
			// Both have the same version string but different MVIDs — cross-context casting fails.
			// Fix: invoke ModelBuilder and SnapshotBuilder from the isolated context, then JSON-bridge the result.
			var coreAsm = CliHelper.GetCoreAssembly(assembly, loadContext);
			var mbType = coreAsm.GetType("Sheetly.Core.ModelBuilder")
				?? throw new Exception(CliHelper.VersionMismatchMessage(coreAsm, "Sheetly.Core.ModelBuilder"));
			var modelBuilder = Activator.CreateInstance(mbType)!;

			var onModelCreatingMethod = contextType.GetMethod("OnModelCreating", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			onModelCreatingMethod?.Invoke(context, [modelBuilder]);

			// Build current snapshot inside the isolated context, then bridge to CLI via JSON
			var sbType = coreAsm.GetType("Sheetly.Core.Migrations.SnapshotBuilder")
				?? throw new Exception(CliHelper.VersionMismatchMessage(coreAsm, "Sheetly.Core.Migrations.SnapshotBuilder"));
			var buildMethod = sbType.GetMethod("BuildFromContext", BindingFlags.Public | BindingFlags.Static)
				?? throw new Exception(CliHelper.VersionMismatchMessage(coreAsm, "SnapshotBuilder.BuildFromContext"));
			var metadataArg = mbType.GetMethod("GetMetadata")!.Invoke(modelBuilder, null);
			var isolatedSnapshot = buildMethod.Invoke(null, [contextType, metadataArg]);
			var currentSnapshot = CliHelper.BridgeFromJson<MigrationSnapshot>(isolatedSnapshot)
				?? throw new Exception("Failed to build migration snapshot.");

			string finalPath = Path.Combine(contextProjectDir, outputDir);
			Directory.CreateDirectory(finalPath);

			MigrationSnapshot? previousSnapshot = null;
			string snapshotClassName = $"{contextType.Name.Replace("Context", "")}ModelSnapshot";
			var snapshotType = assembly.GetExportedTypes()
				.FirstOrDefault(t => t.Name == snapshotClassName && t.Namespace == $"{contextType.Namespace}.Migrations");

			if (snapshotType != null)
			{
				var isolatedPrev = Activator.CreateInstance(snapshotType);
				previousSnapshot = CliHelper.BridgeFromJson<MigrationSnapshot>(isolatedPrev);
			}

			var modelDiffer = new ModelDiffer();
			var operations = modelDiffer.GetDifferences(previousSnapshot, currentSnapshot);

			if (operations.Count == 0)
			{
				Console.WriteLine("⚠️ No changes detected in the model.");
				return;
			}


			var existingMigration = Directory.GetFiles(finalPath, "*.cs")
				.Where(f => !f.Contains("ModelSnapshot"))
				.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).EndsWith($"_{name}", StringComparison.OrdinalIgnoreCase));

			if (existingMigration != null)
			{
				Console.WriteLine($"❌ A migration named '{name}' already exists: '{Path.GetFileName(existingMigration)}'");
				return;
			}

			string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
			string migrationId = $"{timestamp}_{name}";
			string targetNamespace = $"{contextType.Namespace}.Migrations";

			var generator = new CSharpMigrationGenerator();
			string migrationCode = generator.GenerateMigration(name, migrationId, targetNamespace, operations);

			string csharpFileName = $"{migrationId}.cs";
			await File.WriteAllTextAsync(Path.Combine(finalPath, csharpFileName), migrationCode, ct);

			var snapshotGenerator = new ModelSnapshotGenerator();
			string snapshotCode = snapshotGenerator.GenerateModelSnapshot(
				currentSnapshot,
				targetNamespace,
				contextType.Name.Replace("Context", ""));

			string snapshotFileName = $"{contextType.Name.Replace("Context", "")}ModelSnapshot.cs";
			string snapshotFilePath = Path.Combine(finalPath, snapshotFileName);
			await File.WriteAllTextAsync(snapshotFilePath, snapshotCode, ct);

			Console.WriteLine($"✅ Migration created: '{csharpFileName}'");
			Console.WriteLine($"✅ Model snapshot updated: '{snapshotFileName}'");
			Console.WriteLine($"   Operations: {operations.Count}");

			foreach (var op in operations)
			{
				Console.WriteLine($"   - {op.OperationType}");
			}
		}
		catch (Exception ex) { Console.WriteLine($"❌ Error: {ex.Message}"); }
	}
}