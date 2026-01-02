using System.CommandLine;
using System.Reflection;
using System.Text.Json;
using Sheetly.Core;
using Sheetly.Core.Migration;
using Sheetly.CLI.Helpers;

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
			Console.Write("Migratsiya nomini kiriting: ");
			name = Console.ReadLine();
			if (string.IsNullOrWhiteSpace(name)) return;
		}

		string dllPath = CliHelper.FindProjectDll(noBuild, projectPath);
		if (string.IsNullOrEmpty(dllPath)) return;

		try
		{
			var assembly = Assembly.LoadFrom(Path.GetFullPath(dllPath));
			var contextType = assembly.GetExportedTypes().FirstOrDefault(t => CliHelper.IsSubclassOfSheetsContext(t))
				?? throw new Exception("SheetsContext topilmadi.");

			var context = Activator.CreateInstance(contextType)!;
			string contextProjectDir = CliHelper.FindProjectRootFromDll(contextType.Assembly.Location);

			outputDir ??= "Migrations";
			var modelBuilder = new ModelBuilder();
			var onModelCreatingMethod = contextType.GetMethod("OnModelCreating", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			onModelCreatingMethod?.Invoke(context, [modelBuilder]);

			var snapshot = MigrationBuilder.BuildFromContext(contextType, modelBuilder);

			string finalPath = Path.Combine(contextProjectDir, outputDir);
			Directory.CreateDirectory(finalPath);

			var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
			string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

			await File.WriteAllTextAsync(Path.Combine(finalPath, $"{timestamp}_{name}.json"), JsonSerializer.Serialize(snapshot, jsonOptions), ct);
			await File.WriteAllTextAsync(Path.Combine(finalPath, "sheetly_snapshot.json"), JsonSerializer.Serialize(snapshot, jsonOptions), ct);

			Console.WriteLine($"✅ Migratsiya yaratildi: '{finalPath}'");
		}
		catch (Exception ex) { Console.WriteLine($"❌ Xato: {ex.Message}"); }
	}
}